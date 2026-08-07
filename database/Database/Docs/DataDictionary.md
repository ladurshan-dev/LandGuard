# LandGuard — Data Dictionary

Database: **LandGuardDB** · Microsoft SQL Server 2019/2022 Express (local)
Schema: `dbo` · Collation: server default · 12 tables

Columns marked **[ext]** are extensions beyond the normalised relation schema in
Chapter 3.1.3. Everything else matches the documented schema.

> The ER diagram specifies `DATE` for date columns. They are implemented as
> `DATETIME2(0)` so events within a single day can be ordered — required for the
> notification feed and the admin audit trail.

---

## 1. Users  *(ER entity: USER)*

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| UserID | INT IDENTITY | No | PK | Unique user identifier |
| Name | NVARCHAR(150) | No | | Full name |
| Email | NVARCHAR(150) | No | UK | Login identifier |
| PasswordHash | NVARCHAR(255) | No | | BCrypt hash produced by the API |
| NIC | VARCHAR(20) | Yes | UK* | National Identity Card number |
| Phone | VARCHAR(20) | Yes | | Contact number |
| Role | VARCHAR(20) | No | | `Buyer` / `Seller` / `Admin` |
| CreatedAt | DATETIME2(0) | No | | Registration timestamp |
| IsActive | BIT | No | | 0 = suspended by an admin |
| NICVerified **[ext]** | BIT | No | | FR02 seller verification status |

\* NIC uniqueness uses the **filtered** index `UX_Users_NIC ... WHERE NIC IS NOT NULL`.
A plain UNIQUE constraint would allow only one buyer without a NIC, because SQL
Server treats NULLs as equal inside a UNIQUE constraint.

**Constraints**

- `CK_Users_Role` — role must be Buyer, Seller or Admin
- `CK_Users_Email_Format` — must look like an address
- `CK_Users_NIC_Format` — 9 digits + V/X, or 12 digits, or NULL
- `CK_Users_Seller_NIC` — a Seller must always have a NIC

---

## 2. Property

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| PropertyID | INT IDENTITY | No | PK | Unique listing identifier |
| SellerID | INT | No | FK → Users | Owner of the listing |
| Title | NVARCHAR(200) | No | | Listing headline |
| Description | NVARCHAR(MAX) | Yes | | Full description |
| Location | NVARCHAR(255) | No | | Free-text location as typed |
| District **[ext]** | NVARCHAR(100) | Yes | | Normalised district, used for price benchmarking |
| Latitude **[ext]** | DECIMAL(9,6) | Yes | | Written back from the Nominatim API |
| Longitude **[ext]** | DECIMAL(9,6) | Yes | | Written back from the Nominatim API |
| Size | FLOAT | No | | Land size in perches |
| Price | DECIMAL(14,2) | No | | Asking price in LKR |
| PricePerPerch **[ext]** | DECIMAL(14,2) | — | | Computed **persisted**: `Price / Size` |
| DeedReference | VARCHAR(100) | Yes | | Deed reference number |
| Status | VARCHAR(20) | No | | `Pending` / `Approved` / `Flagged` / `Rejected` |
| UploadDate | DATETIME2(0) | No | | Submission timestamp |

**Constraints:** `CK_Property_Status`, `CK_Property_Price` (> 0), `CK_Property_Size` (> 0)

**Status lifecycle**

```
Pending ──fraud engine──> Low risk      ──> Approved  (published automatically)
                       └> Medium/High  ──> Flagged   ──admin──> Approved | Rejected
Rejected ──seller edits and resubmits──> Pending
```

---

## 3. PropertyImage

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| ImageID | INT IDENTITY | No | PK | Unique image identifier |
| PropertyID | INT | No | FK → Property (CASCADE) | Parent listing |
| ImageURL | NVARCHAR(500) | No | | Stored file path |
| ImageHash | VARCHAR(255) | Yes | | Fingerprint — input to fraud rule 2 |
| IsPrimary **[ext]** | BIT | No | | Cover image flag |
| UploadedDate | DATETIME2(0) | No | | Upload timestamp |

---

## 4. FraudCheck

One row per analysis run. **`1` means the rule fired (fraud indicator detected);
`0` means it passed.**

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| FraudCheckID | INT IDENTITY | No | PK | Unique analysis run |
| PropertyID | INT | No | FK → Property (CASCADE) | Listing analysed |
| PriceCheck | BIT | No | | Rule 1 — price anomaly |
| DuplicateCheck | BIT | No | | Rule 2 — duplicate image |
| NICCheck | BIT | No | | Rule 3 — seller NIC verification |
| DeedCheck | BIT | No | | Rule 4 — deed reference duplicate |
| SellerHistoryCheck | BIT | No | | Rule 5 — seller history |
| LocationCheck | BIT | No | | Rule 6 — location validation |
| MissingInfoCheck | BIT | No | | Rule 7 — missing information |
| FraudStatus | VARCHAR(20) | No | | `Clean` / `Suspicious` / `Fraudulent` |
| CheckDate | DATETIME2(0) | No | | When the run happened |

A property can have several rows — one per resubmission. `vw_PropertyLatestRisk`
returns the current one.

---

## 5. RiskReport

Point 8 of the engine. Exactly one row per `FraudCheck`.

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| ReportID | INT IDENTITY | No | PK | Unique report identifier |
| FraudCheckID | INT | No | FK **UNIQUE** → FraudCheck (CASCADE) | 1:1 link |
| RiskScore | INT | No | | 0–100, sum of the weights that fired |
| RiskLevel | VARCHAR(20) | No | | `Low` / `Medium` / `High` |
| Summary | NVARCHAR(MAX) | Yes | | Human-readable fraud report (FR06) |
| GeneratedDate | DATETIME2(0) | No | | Generation timestamp |

**`CK_RiskReport_Banding`** enforces FR05 in the database itself: Low must be
0–40, Medium 41–70, High 71–100. A miscalculated score cannot be stored.

No `PropertyID` column — it is transitive through `FraudCheckID` (3NF, Chapter 3.1.2).

---

## 6. SuspiciousReport

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| SuspiciousReportID | INT IDENTITY | No | PK | Unique report identifier |
| BuyerID | INT | No | FK → Users | Buyer who reported |
| PropertyID | INT | No | FK → Property (CASCADE) | Listing reported |
| Reason | NVARCHAR(255) | No | | Short reason |
| Description | NVARCHAR(MAX) | Yes | | Additional detail |
| ReportDate | DATETIME2(0) | No | | When it was filed |
| Status | VARCHAR(20) | No | | `Open` / `Under Review` / `Resolved` |

`UQ_SuspiciousReport_Once (BuyerID, PropertyID, Reason)` stops one buyer filing
the same complaint repeatedly.

---

## 7. Notification

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| NotificationID | INT IDENTITY | No | PK | Unique notification identifier |
| UserID | INT | No | FK → Users (CASCADE) | Recipient |
| Message | NVARCHAR(500) | No | | Message text |
| NotificationDate | DATETIME2(0) | No | | When it was raised |
| Status | VARCHAR(20) | No | | `Read` / `Unread` |
| RelatedPropertyID **[ext]** | INT | Yes | FK → Property | Deep link target |

---

## 8. Podcast

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| PodcastID | INT IDENTITY | No | PK | Unique podcast identifier |
| AdminID | INT | No | FK → Users | Uploading admin |
| Title | NVARCHAR(200) | No | | Episode title |
| Language | VARCHAR(50) | No | | `English` / `Sinhala` / `Tamil` |
| Description | NVARCHAR(MAX) | Yes | | Episode description |
| AudioURL | NVARCHAR(500) | No | | Audio file path |
| UploadDate | DATETIME2(0) | No | | Upload timestamp |

Titles and descriptions are `NVARCHAR`, so Sinhala and Tamil text is stored
natively (NFR06).

---

## 9. SavedProperty **[ext — FR07]**

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| SavedPropertyID | INT IDENTITY | No | PK | Unique identifier |
| BuyerID | INT | No | FK → Users | Buyer |
| PropertyID | INT | No | FK → Property (CASCADE) | Saved listing |
| SavedDate | DATETIME2(0) | No | | When it was saved |

`UQ_SavedProperty_Pair (BuyerID, PropertyID)` prevents duplicates.

---

## 10. AdminAction **[ext — FR09 / NFR02]**

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| AdminActionID | INT IDENTITY | No | PK | Unique identifier |
| AdminID | INT | No | FK → Users | Acting administrator |
| ActionType | VARCHAR(30) | No | | See list below |
| PropertyID | INT | Yes | FK → Property | Listing acted on |
| TargetUserID | INT | Yes | FK → Users | User acted on |
| ReportID | INT | Yes | FK → SuspiciousReport | Report acted on |
| Remarks | NVARCHAR(500) | Yes | | Reason recorded by the admin |
| ActionDate | DATETIME2(0) | No | | When the action happened |

**ActionType values:** `ApproveListing`, `RejectListing`, `FlagListing`,
`SuspendUser`, `ReactivateUser`, `VerifyNIC`, `ResolveReport`, `RemoveListing`

---

## 11. PriceBenchmark **[ext]**

Reference market rate per perch by district — the baseline for fraud rule 1.

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| BenchmarkID | INT IDENTITY | No | PK | Unique identifier |
| District | NVARCHAR(100) | No | UK | District name |
| MarketPricePerPerch | DECIMAL(14,2) | No | | Indicative rate in LKR |
| UpdatedDate | DATETIME2(0) | No | | Last revision |

If a district has no benchmark row, the engine falls back to the average price
per perch of approved listings in that district.

---

## 12. FraudRuleWeight **[ext]**

Configuration for the rule engine. Weights total 100.

| Column | Type | Null | Key | Description |
|---|---|---|---|---|
| RuleCode | VARCHAR(30) | No | PK | Rule identifier |
| RuleName | NVARCHAR(100) | No | | Display name |
| Weight | INT | No | | Points added when the rule fires |
| Threshold | DECIMAL(9,4) | Yes | | Rule-specific tuning value |
| IsEnabled | BIT | No | | 0 disables the rule (contributes 0) |
| Description | NVARCHAR(400) | Yes | | Shown to buyers in the fraud report |

| RuleCode | Weight | Threshold |
|---|---:|---:|
| NIC_VERIFICATION | 20 | — |
| DEED_DUPLICATE | 20 | — |
| IMAGE_DUPLICATE | 15 | — |
| PRICE_ANOMALY | 15 | 0.40 |
| SELLER_HISTORY | 12 | 2 |
| LOCATION_INVALID | 10 | — |
| MISSING_INFO | 8 | — |

---

## Views

| View | Purpose |
|---|---|
| `vw_PropertyLatestRisk` | Most recent fraud check + risk report per property |
| `vw_PropertyListing` | Listing + seller + risk badge + cover image |
| `vw_PublishedProperty` | Approved listings from active sellers (buyer-facing) |
| `vw_FraudCheckDetail` | Rule-by-rule breakdown — the fraud report (FR06) |
| `vw_FlaggedProperty` | Admin review queue |
| `vw_SellerDashboard` | Per-seller listing counts and average risk (FR08) |
| `vw_BuyerSavedProperty` | Saved listings with current risk (FR07) |
| `vw_FraudStatistics` | Single-row admin dashboard summary |
| `vw_RuleTriggerFrequency` | How often each rule fires — threshold tuning evidence |

## Indexes

17 indexes. Highlights:

| Index | Supports |
|---|---|
| `UX_Users_NIC` (filtered unique) | FR02 — one NIC per account, NULLs allowed |
| `IX_Property_Status_UploadDate` | FR10 — the main buyer search feed |
| `IX_Property_District_Price` | FR10 — district + price band filtering |
| `IX_Property_DeedReference` (filtered) | Fraud rule 4 — deed duplicate lookup |
| `IX_PropertyImage_Hash` (filtered) | Fraud rule 2 — duplicate image lookup |
| `IX_Property_PricePerPerch` | Fraud rule 1 — benchmark comparison |
| `IX_FraudCheck_Property_Date` | Latest-analysis lookup |
| `IX_RiskReport_Level_Score` | FR10 — filter by risk level |
| `IX_Notification_User_Status` | Notification bell, unread first |
