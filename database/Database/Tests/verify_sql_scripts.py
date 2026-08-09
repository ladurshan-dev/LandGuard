#!/usr/bin/env python3
"""
LandGuard - static validation of the T-SQL scripts
==================================================
Catches the mistakes that only surface when the scripts are run against a real
SQL Server instance:

  1. every dbo.<object> that is referenced is also created somewhere
  2. every column named in an INSERT column list exists on that table
  3. BEGIN / END and parentheses are balanced inside every batch
  4. every EXEC'd stored procedure exists
  5. every FOREIGN KEY target table and column exists
  6. objects are created before the script that references them (run order)

Run:  python verify_sql_scripts.py
"""

import re
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "Scripts"
ORDER = [
    "01_Schema.sql",
    "02_Indexes.sql",
    "03_Views.sql",
    "04_StoredProcedures.sql",
    "05_SeedData.sql",
    "06_TestQueries.sql",
]

# T-SQL keywords that follow "dbo." style patterns but are not objects
RESERVED_AFTER_DOT = set()


def strip_comments(sql: str) -> str:
    sql = re.sub(r"/\*.*?\*/", "", sql, flags=re.S)
    sql = re.sub(r"--[^\n]*", "", sql)
    return sql


def strip_strings(sql: str) -> str:
    return re.sub(r"'(?:[^']|'')*'", "''", sql)


def load(name: str):
    p = SCRIPTS / name
    if not p.exists():
        return None, None
    raw = p.read_text(encoding="utf-8")
    return raw, strip_strings(strip_comments(raw))


def main() -> int:
    problems, notes = [], []

    created = {}          # object name (lower) -> script index where created
    tables = {}           # table name (lower) -> [columns]
    procs = set()
    files = {}

    # ---------------------------------------------------------------- parse
    for idx, name in enumerate(ORDER):
        raw, clean = load(name)
        if raw is None:
            problems.append(f"missing script: {name}")
            continue
        files[name] = (raw, clean)

        for m in re.finditer(
            r"CREATE\s+(?:OR\s+ALTER\s+)?(TABLE|VIEW|PROCEDURE|FUNCTION)\s+dbo\.(\w+)",
            clean, re.I,
        ):
            kind, obj = m.group(1).upper(), m.group(2).lower()
            created[obj] = idx
            if kind == "PROCEDURE":
                procs.add(obj)

        # column lists of every CREATE TABLE
        for m in re.finditer(
            r"CREATE\s+TABLE\s+dbo\.(\w+)\s*\((.*?)\n\);", clean, re.I | re.S
        ):
            tname = m.group(1).lower()
            cols = []
            depth = 0
            for line in m.group(2).splitlines():
                s = line.strip()
                depth += s.count("(") - s.count(")")
                if not s or s.upper().startswith("CONSTRAINT"):
                    continue
                cm = re.match(r"(\w+)\s+", s)
                if cm and cm.group(1).upper() not in (
                    "CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "REFERENCES",
                    "ON", "AS", "WHEN", "THEN", "ELSE", "END", "CASE",
                ):
                    cols.append(cm.group(1).lower())
            tables[tname] = cols

    # columns added later via ALTER TABLE ADD (none expected, but be safe)
    for name, (raw, clean) in files.items():
        for m in re.finditer(r"ALTER\s+TABLE\s+dbo\.(\w+)\s+ADD\s+(\w+)\s+\w", clean, re.I):
            t, c = m.group(1).lower(), m.group(2).lower()
            if c != "constraint" and t in tables and c not in tables[t]:
                tables[t].append(c)

    # ------------------------------------------------- 1 & 6 object refs
    for idx, name in enumerate(ORDER):
        if name not in files:
            continue
        _, clean = files[name]
        for m in re.finditer(r"dbo\.(\w+)", clean):
            obj = m.group(1).lower()
            if obj in ("fn_isvalidnic", "fn_riskl", ):
                pass
            if obj not in created:
                problems.append(f"{name}: references dbo.{m.group(1)} which is never created")
            elif created[obj] > idx:
                problems.append(
                    f"{name}: references dbo.{m.group(1)} created later in "
                    f"{ORDER[created[obj]]} (run-order problem)"
                )

    # ------------------------------------------------- 2 insert column lists
    for name, (raw, clean) in files.items():
        for m in re.finditer(
            r"INSERT\s+INTO\s+dbo\.(\w+)\s*\(([^)]*)\)", clean, re.I | re.S
        ):
            t = m.group(1).lower()
            if t not in tables:
                continue
            for col in [c.strip().lower() for c in m.group(2).split(",") if c.strip()]:
                if col not in tables[t]:
                    problems.append(f"{name}: INSERT INTO {m.group(1)} names unknown column '{col}'")

    # ------------------------------------------------- 3 BEGIN/END + parens
    for name, (raw, clean) in files.items():
        for batch in re.split(r"^\s*GO\s*$", clean, flags=re.M | re.I):
            if not batch.strip():
                continue
            # BEGIN...END blocks. Excluded from the count:
            #   BEGIN TRANSACTION / BEGIN TRAN  -> closed by COMMIT or ROLLBACK
            #   CASE ... END                    -> an END with no matching BEGIN
            # BEGIN TRY / BEGIN CATCH do pair with END TRY / END CATCH, so they
            # are left in the count on both sides.
            n_begin = len(re.findall(r"\bBEGIN\b", batch, re.I))
            n_end = len(re.findall(r"\bEND\b", batch, re.I))
            n_trans = len(re.findall(r"\bBEGIN\s+TRAN(?:SACTION)?\b", batch, re.I))
            n_case = len(re.findall(r"\bCASE\b", batch, re.I))
            delta = (n_begin - n_trans) - (n_end - n_case)
            if delta != 0:
                head = re.search(r"(CREATE[^\n]*)", batch, re.I)
                problems.append(
                    f"{name}: unbalanced BEGIN/END in batch starting "
                    f"'{(head.group(1)[:60] if head else batch.strip()[:60])}' (delta={delta})"
                )
            if batch.count("(") != batch.count(")"):
                head = re.search(r"(CREATE[^\n]*)", batch, re.I)
                problems.append(
                    f"{name}: unbalanced parentheses in batch "
                    f"'{(head.group(1)[:60] if head else batch.strip()[:60])}'"
                )

    # ------------------------------------------------- 4 EXEC targets
    for name, (raw, clean) in files.items():
        for m in re.finditer(r"EXEC(?:UTE)?\s+dbo\.(\w+)", clean, re.I):
            if m.group(1).lower() not in procs:
                problems.append(f"{name}: EXEC dbo.{m.group(1)} - procedure not defined")

    # ------------------------------------------------- 5 FK targets
    _, schema = files.get("01_Schema.sql", (None, ""))
    for m in re.finditer(r"REFERENCES\s+dbo\.(\w+)\s*\((\w+)\)", schema or "", re.I):
        t, c = m.group(1).lower(), m.group(2).lower()
        if t not in tables:
            problems.append(f"01_Schema.sql: FK references unknown table {m.group(1)}")
        elif c not in tables[t]:
            problems.append(f"01_Schema.sql: FK references unknown column {m.group(1)}.{m.group(2)}")

    # ------------------------------------------------- report
    print("=" * 78)
    print("LANDGUARD - T-SQL SCRIPT VALIDATION")
    print("=" * 78)
    print(f"Scripts checked      : {len(files)}")
    print(f"Tables created       : {len(tables)}")
    print(f"Views created        : {sum(1 for o in created if o.startswith('vw_'))}")
    print(f"Procedures created   : {len(procs)}")
    print(f"Functions created    : {sum(1 for o in created if o.startswith('fn_'))}")
    print()
    for t in sorted(tables):
        print(f"  {t:<20} {len(tables[t]):>2} columns")
    print()

    if problems:
        print(f"{len(problems)} PROBLEM(S) FOUND")
        for p in dict.fromkeys(problems):
            print("  x", p)
    else:
        print("No problems found: object references, run order, insert columns,")
        print("BEGIN/END nesting, parentheses, EXEC targets and FK targets all resolve.")

    for n in notes:
        print("  i", n)

    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
