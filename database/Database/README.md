# LandGuard — Database

Land Transaction System with Fraud Detection
Group 08 · ICBT · HD in Computing and Software Engineering

Database owner: **Ladhurshan Sivasathyamoorthy** (Backend & Database Developer / QA)

---

## What this is

A complete, local **Microsoft SQL Server** database for LandGuard: schema, indexes,
views, the 8-point fraud detection engine implemented as stored procedures, and a
dummy dataset that exercises every rule.

Everything runs on a local SQL Server Express / LocalDB instance. No cloud
services, no external accounts.

---

## Quick start

**Prerequisites:** SQL Server 2019 or 2022 Express (or LocalDB) and SSMS.

### SSMS

1. Connect to your local instance — `localhost\SQLEXPRESS` or `(localdb)\MSSQLLocalDB`.
2. **Query → SQLCMD Mode** (required — the runner uses `:r` includes).
3. Open `Scripts/00_RunAll.sql` and press **F5**.
4. Open `Scripts/06_TestQueries.sql` and press **F5** to verify.

### Command line

```cmd
cd Scripts
sqlcmd -S localhost\SQLEXPRESS -E -f 65001 -i 00_RunAll.sql
sqlcmd -S localhost\SQLEXPRESS -E -f 65001 -i 06_TestQueries.sql
```

`-f 65001` sets the UTF-8 code page. Without it the Sinhala and Tamil podcast
rows load as question marks.

> `00_RunAll.sql` **drops and recreates** `LandGuardDB`. It is meant for a clean
> local rebuild, not for a database with real data in it.

### Connection string for the ASP.NET Core Web API

```json
{
  "ConnectionStrings": {
    "LandGuardDB": "Server=localhost\\SQLEXPRESS;Database=LandGuardDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```

---

## Files

| File | Contents |
|---|---|
| `Scripts/00_RunAll.sql` | Runs scripts 01–05 in order |
| `Scripts/01_Schema.sql` | Database + 12 tables, keys, CHECK constraints |
| `Scripts/02_Indexes.sql` | 17 indexes for search, filtering and fraud lookups |
| `Scripts/03_Views.sql` | 9 views — one per screen in the frontend |
| `Scripts/04_StoredProcedures.sql` | 3 functions + 29 stored procedures |
| `Scripts/05_SeedData.sql` | 16 users, 31 listings, 39 images, podcasts, reports |
| `Scripts/06_TestQueries.sql` | 10 verification tests + worked API examples |
| `Docs/DataDictionary.md` | Every table and column explained |
| `Docs/ERD.md` | ER diagram (Mermaid) + relationship list |
| `Docs/FraudEngine.md` | The 8 points, weights, thresholds, worked examples |
| `Docs/API_Mapping.md` | Endpoint → stored procedure mapping |
| `Tests/verify_sql_scripts.py` | Static validation of the T-SQL |
| `Tests/verify_fraud_engine.py` | Re-scores the seed data and checks every result |

---

## Schema

Eight tables come straight from the normalised relation schema in Chapter 3.1.3.
Four more support requirements that the ER diagram did not cover.

**From the ER diagram**

`Users` · `Property` · `PropertyImage` · `FraudCheck` · `RiskReport` ·
`SuspiciousReport` · `Notification` · `Podcast`

**Supporting tables**

| Table | Why it exists |
|---|---|
| `SavedProperty` | FR07 — the buyer dashboard's saved listings |
| `AdminAction` | FR09 / NFR02 — audit trail of every admin decision |
| `PriceBenchmark` | Reference rate per perch per district, used by the price anomaly rule |
| `FraudRuleWeight` | Rule weights and thresholds, so the engine can be re-tuned without redeploying the API |

The USER entity is physically named `Users` because `USER` is a reserved T-SQL
keyword. Structure is otherwise identical to the documented schema.

`RiskReport` deliberately does **not** store `PropertyID` — it is reachable
through `FraudCheckID → PropertyID`, and duplicating it would reintroduce the
transitive dependency removed in 3NF (Chapter 3.1.2).

---

## The 8-point fraud engine

Implemented in `usp_Fraud_AnalyseProperty` (rules 1–7) and
`usp_Risk_GenerateReport` (point 8, the combined score).

| # | Rule | Weight | Fires when |
|---|---|---:|---|
| 1 | Price Anomaly | 15 | Price per perch is >40% below the district benchmark |
| 2 | Duplicate Image | 15 | An image hash already exists on another property |
| 3 | NIC Verification | 20 | Seller NIC missing, malformed, unverified or shared |
| 4 | Deed Reference Duplicate | 20 | Same deed reference on another live listing |
| 5 | Seller History | 12 | Seller has 2+ rejected listings or upheld reports |
| 6 | Location Validation | 10 | Coordinates missing or outside Sri Lanka |
| 7 | Missing Information | 8 | Deed, description, images, district or phone absent |
| 8 | **Risk Score** | **100** | Sum of the weights above |

Weights total exactly 100, so the score always lands in the FR05 range.

**Banding (FR05):** Low 0–40 · Medium 41–70 · High 71–100

A `Low` listing publishes automatically. `Medium` and `High` go to the admin
review queue. `High` also raises a notification for every admin.

Weights and thresholds live in `dbo.FraudRuleWeight`, not in code. An admin can
retune a rule and re-score the whole dataset with:

```sql
EXEC dbo.usp_Admin_UpdateRuleWeight
     @RuleCode = 'PRICE_ANOMALY', @Weight = 10, @Reanalyse = 1;
```

This is the mitigation for the first item in the Chapter 3.3 Risk Analysis —
thresholds can be adjusted from test results without touching the API.

---

## Seed data

31 listings deliberately spread across all three risk bands, including the
band boundaries.

| Risk level | Listings | Notable cases |
|---|---:|---|
| Low (0–40) | 24 | P1–P3 score 0; P12/P13 sit exactly on the 40 boundary |
| Medium (41–70) | 3 | P28 sits exactly on the 70 boundary |
| High (71–100) | 4 | P30 sits on the 71+ boundary; **P21 fires all seven rules for 100** |

Planted for testing: 7 duplicate deed-reference pairs, 5 duplicate-image pairs,
5 listings with unresolvable locations, 9 price anomalies, 2 sellers with a
rejection history, 1 seller with an unverified NIC, 1 seller with no phone.

Every seeded password hash is BCrypt of `Test@123`. **Local testing only.**

Test accounts:

| Role | Email | Notes |
|---|---|---|
| Admin | `abilasha@landguard.lk` | Product owner |
| Admin | `ladhurshan@landguard.lk` | Database owner |
| Seller | `chathura@example.com` | Verified, clean listings |
| Seller | `rajitha.bandara@example.com` | NIC initially unverified |
| Seller | `malith.j@example.com` | Two rejected listings |
| Seller | `priyantha.alwis@example.com` | Suspended by admin during seeding |
| Buyer | `sanduni.r@example.com` | Saved properties + filed reports |

---

## Verification

Two harnesses run without a SQL Server instance, so the team can check the
scripts on any machine:

```bash
cd Tests
python verify_sql_scripts.py     # object refs, run order, BEGIN/END, FK targets
python verify_fraud_engine.py    # re-scores all 31 listings from the seed file
```

`verify_fraud_engine.py` loads the seed data straight out of `05_SeedData.sql`,
re-implements the seven rules, and asserts the score and band of every listing.
Current result: **31/31 pass**.

Against a live instance, `06_TestQueries.sql` runs 10 tests covering row counts,
score recomputation, band correctness, duplicate detection, publication rules,
orphan rows and the 1:1 `FraudCheck → RiskReport` relationship.

---

## Known limitations

Carried over from Chapter 2.1 and the Chapter 3.3 risk analysis:

- NIC verification checks **format only**. No government API is available, so
  seller identity is simulated.
- Duplicate-image detection compares hashes **inside LandGuard only**. Images
  copied from external sites are not detected in version 1.
- Location validation depends on the API layer writing back Nominatim
  coordinates before analysis runs.
- Legitimate distress sales can trigger the price rule. The admin approval path
  (`usp_Admin_ApproveProperty`) is the intended appeal route — seeded property
  P28 demonstrates it.
- Password hashes and NIC values are stored in plain columns. Chapter 3.3 flags
  column-level encryption for NIC under the Personal Data Protection Act
  (Act No. 9 of 2022) as a follow-up.
