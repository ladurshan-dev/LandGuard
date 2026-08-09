#!/usr/bin/env python3
"""
LandGuard - fraud engine verification harness
=============================================
Loads the seed data straight out of 05_SeedData.sql into an in-memory SQLite
database, re-implements the 7 rule checks exactly as they are written in
04_StoredProcedures.sql, and asserts that every seeded property produces the
risk score and risk level documented in the seed file.

Run:  python verify_fraud_engine.py
"""

import re
import sqlite3
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "Scripts"
SEED = SCRIPTS / "05_SeedData.sql"

WEIGHTS = {
    "PRICE_ANOMALY": 15,
    "IMAGE_DUPLICATE": 15,
    "NIC_VERIFICATION": 20,
    "DEED_DUPLICATE": 20,
    "SELLER_HISTORY": 12,
    "LOCATION_INVALID": 10,
    "MISSING_INFO": 8,
}
PRICE_THRESHOLD = 0.40
HISTORY_THRESHOLD = 2

# PropertyID -> (expected score, expected level)
EXPECTED = {
    1: (0, "Low"), 2: (0, "Low"), 3: (0, "Low"),
    4: (15, "Low"), 5: (10, "Low"), 6: (8, "Low"),
    7: (15, "Low"), 8: (15, "Low"), 9: (20, "Low"),
    10: (20, "Low"), 11: (20, "Low"),
    12: (40, "Low"), 13: (40, "Low"),
    14: (80, "High"), 15: (60, "Medium"), 16: (20, "Low"),
    17: (42, "Medium"), 18: (15, "Low"), 19: (15, "Low"), 20: (15, "Low"),
    21: (100, "High"), 22: (20, "Low"),
    23: (28, "Low"), 24: (28, "Low"),
    25: (80, "High"), 26: (20, "Low"), 27: (15, "Low"),
    28: (70, "Medium"), 29: (35, "Low"),
    30: (73, "High"), 31: (20, "Low"),
}

DDL = """
CREATE TABLE Users (
    UserID INTEGER PRIMARY KEY, Name TEXT, Email TEXT UNIQUE, PasswordHash TEXT,
    NIC TEXT UNIQUE, Phone TEXT, Role TEXT, IsActive INTEGER, NICVerified INTEGER,
    CreatedAt TEXT);
CREATE TABLE PriceBenchmark (
    District TEXT PRIMARY KEY, MarketPricePerPerch REAL);
CREATE TABLE Property (
    PropertyID INTEGER PRIMARY KEY, SellerID INTEGER, Title TEXT, Description TEXT,
    Location TEXT, District TEXT, Latitude REAL, Longitude REAL, Size REAL,
    Price REAL, DeedReference TEXT, Status TEXT, UploadDate TEXT,
    FOREIGN KEY (SellerID) REFERENCES Users(UserID));
CREATE TABLE PropertyImage (
    ImageID INTEGER PRIMARY KEY AUTOINCREMENT, PropertyID INTEGER, ImageURL TEXT,
    ImageHash TEXT, IsPrimary INTEGER,
    FOREIGN KEY (PropertyID) REFERENCES Property(PropertyID));
"""

# ---------------------------------------------------------------------------
# 1. Pull the INSERT statements we need out of the T-SQL seed script
# ---------------------------------------------------------------------------


def strip_comments(sql: str) -> str:
    sql = re.sub(r"/\*.*?\*/", "", sql, flags=re.S)
    sql = re.sub(r"--[^\n]*", "", sql)
    return sql


def extract_inserts(sql: str, table: str):
    """Return the full text of every INSERT INTO dbo.<table> ... ; statement."""
    out = []
    pattern = re.compile(r"INSERT\s+INTO\s+dbo\." + table + r"\b", re.I)
    for m in pattern.finditer(sql):
        start = m.start()
        # statement ends at the first ';' that is not inside a string literal
        i, in_str = start, False
        while i < len(sql):
            ch = sql[i]
            if ch == "'":
                in_str = not in_str
            elif ch == ";" and not in_str:
                break
            i += 1
        out.append(sql[start:i])
    return out


def to_sqlite(stmt: str) -> str:
    stmt = stmt.replace("dbo.", "")
    stmt = re.sub(r"(?<![A-Za-z0-9_])N'", "'", stmt)      # N'...' -> '...'
    stmt = stmt.replace("[", '"').replace("]", '"')
    return stmt


def load_db() -> sqlite3.Connection:
    raw = SEED.read_text(encoding="utf-8")
    sql = strip_comments(raw)

    con = sqlite3.connect(":memory:")
    con.row_factory = sqlite3.Row
    con.executescript(DDL)

    for table in ("PriceBenchmark", "Users", "Property", "PropertyImage"):
        stmts = extract_inserts(sql, table)
        if not stmts:
            raise SystemExit(f"FAIL: no INSERT statements found for {table}")
        for s in stmts:
            con.execute(to_sqlite(s))
    con.commit()
    return con


# ---------------------------------------------------------------------------
# 2. The 7 rules, mirroring usp_Fraud_AnalyseProperty
# ---------------------------------------------------------------------------


def is_valid_nic(nic) -> bool:
    if not nic:
        return False
    nic = nic.strip()
    if re.fullmatch(r"\d{9}[VvXx]", nic):
        return True
    return bool(re.fullmatch(r"\d{12}", nic))


def analyse(con: sqlite3.Connection, pid: int) -> dict:
    p = con.execute("SELECT * FROM Property WHERE PropertyID=?", (pid,)).fetchone()
    s = con.execute("SELECT * FROM Users WHERE UserID=?", (p["SellerID"],)).fetchone()
    fired = {}

    # CHECK 1 - price anomaly
    ppp = p["Price"] / p["Size"] if p["Size"] else None
    bm = con.execute(
        "SELECT MarketPricePerPerch FROM PriceBenchmark WHERE District=?",
        (p["District"],),
    ).fetchone()
    benchmark = bm["MarketPricePerPerch"] if bm else None
    if benchmark is None:
        row = con.execute(
            "SELECT AVG(Price/Size) v FROM Property WHERE District=? AND Status='Approved'"
            " AND PropertyID<>?",
            (p["District"], pid),
        ).fetchone()
        benchmark = row["v"]
    fired["PRICE_ANOMALY"] = bool(
        benchmark is not None and ppp is not None and ppp < benchmark * (1 - PRICE_THRESHOLD)
    )

    # CHECK 2 - duplicate image
    fired["IMAGE_DUPLICATE"] = bool(
        con.execute(
            "SELECT 1 FROM PropertyImage a JOIN PropertyImage b"
            " ON b.ImageHash=a.ImageHash AND b.PropertyID<>a.PropertyID"
            " WHERE a.PropertyID=? AND a.ImageHash IS NOT NULL LIMIT 1",
            (pid,),
        ).fetchone()
    )

    # CHECK 3 - seller NIC verification
    shared = con.execute(
        "SELECT 1 FROM Users WHERE NIC=? AND UserID<>? LIMIT 1", (s["NIC"], s["UserID"])
    ).fetchone()
    fired["NIC_VERIFICATION"] = bool(
        s["NIC"] is None
        or not is_valid_nic(s["NIC"])
        or s["NICVerified"] == 0
        or s["IsActive"] == 0
        or shared
    )

    # CHECK 4 - deed reference duplicate
    fired["DEED_DUPLICATE"] = bool(
        p["DeedReference"]
        and con.execute(
            "SELECT 1 FROM Property WHERE DeedReference=? AND PropertyID<>?"
            " AND Status IN ('Pending','Approved','Flagged') LIMIT 1",
            (p["DeedReference"], pid),
        ).fetchone()
    )

    # CHECK 5 - seller history
    rejected = con.execute(
        "SELECT COUNT(*) c FROM Property WHERE SellerID=? AND Status='Rejected'"
        " AND PropertyID<>?",
        (s["UserID"], pid),
    ).fetchone()["c"]
    fired["SELLER_HISTORY"] = rejected >= HISTORY_THRESHOLD

    # CHECK 6 - location validation (Sri Lanka bounding box)
    lat, lon = p["Latitude"], p["Longitude"]
    fired["LOCATION_INVALID"] = not (
        lat is not None
        and lon is not None
        and 5.9 <= lat <= 9.9
        and 79.6 <= lon <= 81.9
    )

    # CHECK 7 - missing information
    img_count = con.execute(
        "SELECT COUNT(*) c FROM PropertyImage WHERE PropertyID=?", (pid,)
    ).fetchone()["c"]
    fired["MISSING_INFO"] = bool(
        p["DeedReference"] is None
        or p["Description"] is None
        or len(p["Description"].strip()) < 30
        or img_count == 0
        or not (p["District"] or "").strip()
        or not (s["Phone"] or "").strip()
    )

    score = min(100, sum(WEIGHTS[k] for k, v in fired.items() if v))
    level = "Low" if score <= 40 else ("Medium" if score <= 70 else "High")
    return {"fired": fired, "score": score, "level": level}


# ---------------------------------------------------------------------------
# 3. Structural checks on the seed data itself
# ---------------------------------------------------------------------------


def structural_checks(con) -> list:
    problems = []

    orphan_prop = con.execute(
        "SELECT COUNT(*) c FROM Property p LEFT JOIN Users u ON u.UserID=p.SellerID"
        " WHERE u.UserID IS NULL"
    ).fetchone()["c"]
    if orphan_prop:
        problems.append(f"{orphan_prop} property rows reference a missing seller")

    non_seller = con.execute(
        "SELECT COUNT(*) c FROM Property p JOIN Users u ON u.UserID=p.SellerID"
        " WHERE u.Role<>'Seller'"
    ).fetchone()["c"]
    if non_seller:
        problems.append(f"{non_seller} property rows belong to a non-Seller account")

    orphan_img = con.execute(
        "SELECT COUNT(*) c FROM PropertyImage i LEFT JOIN Property p"
        " ON p.PropertyID=i.PropertyID WHERE p.PropertyID IS NULL"
    ).fetchone()["c"]
    if orphan_img:
        problems.append(f"{orphan_img} image rows reference a missing property")

    for r in con.execute("SELECT UserID, Role, NIC FROM Users"):
        if r["Role"] == "Seller" and not is_valid_nic(r["NIC"]):
            problems.append(f"seller {r['UserID']} has an invalid NIC ({r['NIC']})")
        if r["NIC"] and not is_valid_nic(r["NIC"]):
            problems.append(f"user {r['UserID']} NIC fails the CK_Users_NIC_Format check")

    for r in con.execute("SELECT PropertyID, District FROM Property"):
        hit = con.execute(
            "SELECT 1 FROM PriceBenchmark WHERE District=?", (r["District"],)
        ).fetchone()
        if not hit:
            problems.append(f"property {r['PropertyID']} district '{r['District']}' has no benchmark")

    total = sum(WEIGHTS.values())
    if total != 100:
        problems.append(f"rule weights total {total}, expected 100")

    return problems


# ---------------------------------------------------------------------------


def main() -> int:
    con = load_db()

    print("=" * 78)
    print("LANDGUARD - FRAUD ENGINE VERIFICATION")
    print("=" * 78)

    counts = {
        t: con.execute(f"SELECT COUNT(*) c FROM {t}").fetchone()["c"]
        for t in ("Users", "Property", "PropertyImage", "PriceBenchmark")
    }
    print("Seed rows loaded from 05_SeedData.sql:", counts, "\n")

    problems = structural_checks(con)
    if problems:
        print("STRUCTURAL PROBLEMS")
        for p in problems:
            print("  x", p)
        print()
    else:
        print("Structural checks: PASS (referential integrity, NIC formats, "
              "benchmark coverage, weight total)\n")

    hdr = f"{'ID':>3} {'Score':>5} {'Level':<7} {'Expect':>6} {'':<7} {'Rules fired':<52} Result"
    print(hdr)
    print("-" * len(hdr))

    failures = 0
    dist = {"Low": 0, "Medium": 0, "High": 0}

    for pid in sorted(EXPECTED):
        res = analyse(con, pid)
        exp_score, exp_level = EXPECTED[pid]
        ok = res["score"] == exp_score and res["level"] == exp_level
        failures += 0 if ok else 1
        dist[res["level"]] += 1
        rules = ",".join(k for k, v in res["fired"].items() if v) or "-none-"
        print(
            f"{pid:>3} {res['score']:>5} {res['level']:<7} {exp_score:>6} "
            f"{exp_level:<7} {rules:<52} {'PASS' if ok else 'FAIL'}"
        )

    print("-" * len(hdr))
    print(f"Risk distribution: Low={dist['Low']}  Medium={dist['Medium']}  High={dist['High']}")
    print(f"{len(EXPECTED) - failures}/{len(EXPECTED)} properties scored as documented.")

    # boundary coverage - proves the FR05 bands are exercised
    scores = {pid: EXPECTED[pid][0] for pid in EXPECTED}
    for label, val in (("Low upper (40)", 40), ("Medium upper (70)", 70),
                       ("High lower (71+)", 73), ("Maximum (100)", 100)):
        hit = [p for p, s in scores.items() if s == val]
        print(f"  band boundary {label:<18} covered by property {hit}")

    ok = failures == 0 and not problems
    print("\nRESULT:", "ALL CHECKS PASSED" if ok else "FAILURES DETECTED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
