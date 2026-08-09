# LandGuard — The 8-Point Fraud Detection Engine

Implemented entirely in the database:

- `usp_Fraud_AnalyseProperty` — runs rules 1–7 and writes one `FraudCheck` row
- `usp_Risk_GenerateReport` — point 8: combines the results into a risk score,
  bands it, writes the summary, sets the listing status, raises notifications

The API calls `usp_Fraud_AnalyseProperty` on every submission and every
resubmission, which satisfies NFR04.

---

## Rules, weights and thresholds

Weights live in `dbo.FraudRuleWeight` and total exactly **100**.

| # | Rule | Code | Weight | Fires when |
|---|---|---|---:|---|
| 1 | Price Anomaly | `PRICE_ANOMALY` | 15 | `PricePerPerch < benchmark × (1 − 0.40)` |
| 2 | Duplicate Image | `IMAGE_DUPLICATE` | 15 | An `ImageHash` on this listing exists on a different `PropertyID` |
| 3 | NIC Verification | `NIC_VERIFICATION` | 20 | NIC missing, malformed, `NICVerified = 0`, seller suspended, or NIC shared with another account |
| 4 | Deed Reference Duplicate | `DEED_DUPLICATE` | 20 | Same `DeedReference` on another `Pending`/`Approved`/`Flagged` listing |
| 5 | Seller History | `SELLER_HISTORY` | 12 | Seller has ≥ 2 rejected listings, or ≥ 2 resolved reports against their listings |
| 6 | Location Validation | `LOCATION_INVALID` | 10 | Coordinates NULL, or outside 5.9–9.9 °N / 79.6–81.9 °E |
| 7 | Missing Information | `MISSING_INFO` | 8 | No deed, description under 30 characters, no images, no district, or seller has no phone |
| 8 | **Risk Score** | — | **= 100** | Sum of the weights above, capped at 100 |

### Why these weights

The two rules that map most directly to documented Sri Lankan fraud patterns
carry the most weight. Chapter 2.5.2 cites the Registrar General's estimate that
40–50% of land deeds are forged, and multiple sources describe impersonation
using forged NIC details — so **deed duplication** and **NIC verification** are
20 points each. Together they alone reach 40, the top of the Low band, meaning
either one on its own is a warning rather than a verdict, but both together push
a listing to the edge of Medium.

Price and image rules are 15 each: strong signals, but with legitimate
explanations (distress sales, a seller reusing their own photographs).

Location and missing information are the weakest at 10 and 8, because they are
usually carelessness rather than fraud.

Seller history sits at 12 — meaningful corroboration, but it describes the
account rather than this particular listing.

---

## Banding (FR05)

| Band | Score | Listing status | What the buyer sees |
|---|---|---|---|
| **Low** | 0–40 | `Approved` — published automatically | Green badge |
| **Medium** | 41–70 | `Flagged` — admin review queue | Amber badge |
| **High** | 71–100 | `Flagged` + alert to every admin | Red badge |

The banding is enforced by `CK_RiskReport_Banding`, so an incorrectly banded
score cannot be written to the database at all.

---

## Execution flow

```
POST /api/properties
        │
        ▼
usp_Property_Create ──────────► INSERT INTO Property (Status = 'Pending')
        │
        ▼
usp_Fraud_AnalyseProperty
        │
        ├─ CHECK 1  Price Anomaly        → PriceBenchmark by district
        ├─ CHECK 2  Duplicate Image      → PropertyImage.ImageHash self-join
        ├─ CHECK 3  NIC Verification     → Users.NIC + fn_IsValidNIC
        ├─ CHECK 4  Deed Duplicate       → Property.DeedReference
        ├─ CHECK 5  Seller History       → rejected listings + resolved reports
        ├─ CHECK 6  Location Validation  → Latitude / Longitude bounding box
        └─ CHECK 7  Missing Information  → mandatory field completeness
        │
        ▼
   INSERT INTO FraudCheck (7 bit flags)
        │
        ▼
usp_Risk_GenerateReport   ◄── POINT 8
        │
        ├─ score  = Σ weights of the rules that fired  (cap 100)
        ├─ level  = fn_RiskLevelFromScore(score)
        ├─ summary= bulleted list of every rule that fired
        ├─ INSERT INTO RiskReport
        ├─ UPDATE Property.Status  (Low → Approved, otherwise Flagged)
        ├─ NOTIFY seller
        └─ NOTIFY all admins if High
```

---

## Worked examples from the seed data

### Property 1 — clean listing, score 0 (Low)

Verified seller with a phone number, complete description, deed reference,
images, valid Colombo coordinates, priced at the district benchmark.
No rule fires. Published automatically.

### Property 28 — score 70 (Medium, upper boundary)

| Rule | Fired | Points |
|---|---|---:|
| NIC Verification | yes — seller's NIC not yet verified | 20 |
| Deed Duplicate | yes — shares a deed with property 29 | 20 |
| Duplicate Image | yes — shares an image with property 29 | 15 |
| Price Anomaly | yes — 280,000/perch against a 500,000 benchmark | 15 |
| **Total** | | **70 → Medium** |

An admin later approved it on appeal after confirming the discount was a genuine
bank loan settlement — the manual review path from the Chapter 3.3 risk analysis.

### Property 21 — score 100 (High, all seven rules)

| Rule | Points |
|---|---:|
| NIC Verification — unverified seller | 20 |
| Deed Duplicate — same deed as property 22 | 20 |
| Duplicate Image — same image as property 14 | 15 |
| Price Anomaly — 1,000,000/perch in Colombo 07 against 3,500,000 | 15 |
| Seller History — two previously rejected listings | 12 |
| Location Validation — no coordinates | 10 |
| Missing Information — seller has no phone number | 8 |
| **Total** | **100 → High** |

Rejected by an admin; the seller account was suspended.

### Property 30 — score 73 (High, lower boundary)

NIC (20) + Deed (20) + Price (15) + Location (10) + Missing Info (8) = **73**,
one point over the Medium/High boundary. Useful for demonstrating that the
banding is exact.

---

## Retuning without a redeploy

The first risk in Chapter 3.3 is that fraudsters price just above the detection
threshold. Because the weights and thresholds are table data, an admin can react
without any code change:

```sql
-- Tighten the price rule from 40% to 30% below benchmark and re-score everything
EXEC dbo.usp_Admin_UpdateRuleWeight
     @RuleCode  = 'PRICE_ANOMALY',
     @Threshold = 0.30,
     @Reanalyse = 1;

-- Disable a rule that is producing too many false positives
EXEC dbo.usp_Admin_UpdateRuleWeight
     @RuleCode  = 'LOCATION_INVALID',
     @IsEnabled = 0,
     @Reanalyse = 1;
```

`vw_RuleTriggerFrequency` shows how often each rule fires across the whole
dataset — the evidence base for deciding what to change.

---

## Verification

`Tests/verify_fraud_engine.py` loads the seed data out of `05_SeedData.sql`,
re-implements all seven rules independently, and asserts the score and band of
every listing against the values documented in the seed file.

```
31/31 properties scored as documented.
Risk distribution: Low=24  Medium=3  High=4
Band boundaries covered: 40 (P12, P13) · 70 (P28) · 73 (P30) · 100 (P21)
```

Against a live instance, `06_TestQueries.sql` test 2 recomputes every score from
`vw_FraudCheckDetail` and compares it to the stored `RiskScore`, and test 6/7
confirm that every planted duplicate deed and duplicate image was detected.

---

## Known limitations

Carried from Chapter 2.1 and the Chapter 3.3 risk analysis:

1. **NIC verification is format-only.** No government API exists, so identity is
   simulated. `fn_IsValidNIC` validates the old 9-digit + V/X and new 12-digit
   formats and nothing more.
2. **Duplicate images are only compared inside LandGuard.** Images copied from
   external websites are not detected in version 1. Reverse image search is the
   proposed v2 enhancement.
3. **Location validation depends on the API layer.** The engine reads
   `Latitude`/`Longitude`; the ASP.NET Core service must call Nominatim and write
   them back before analysis, otherwise rule 6 fires on a valid address.
4. **Legitimate low prices are flagged.** Distress sales and inherited land can
   trigger rule 1. `usp_Admin_ApproveProperty` is the appeal route.
5. **Seller history needs history.** A brand-new fraudulent account has none, so
   rule 5 never fires on a first offence by design.
