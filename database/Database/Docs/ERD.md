# LandGuard — Entity Relationship Diagram

Physical implementation of the ER model documented in Chapter 3.1.
Paste the Mermaid block below into draw.io, GitHub, or any Mermaid renderer.

## ER diagram

```mermaid
erDiagram
    USERS ||--o{ PROPERTY            : "lists"
    USERS ||--o{ SUSPICIOUS_REPORT   : "files"
    USERS ||--o{ NOTIFICATION        : "receives"
    USERS ||--o{ PODCAST             : "uploads"
    USERS ||--o{ SAVED_PROPERTY      : "saves"
    USERS ||--o{ ADMIN_ACTION        : "performs"

    PROPERTY ||--o{ PROPERTY_IMAGE     : "has"
    PROPERTY ||--o{ FRAUD_CHECK        : "is analysed by"
    PROPERTY ||--o{ SUSPICIOUS_REPORT  : "is reported in"
    PROPERTY ||--o{ SAVED_PROPERTY     : "is saved in"
    PROPERTY ||--o{ ADMIN_ACTION       : "is acted on in"

    FRAUD_CHECK ||--|| RISK_REPORT     : "produces"

    PRICE_BENCHMARK      ||..o{ PROPERTY    : "benchmarks (by district)"
    FRAUD_RULE_WEIGHT    ||..o{ FRAUD_CHECK : "weights"

    USERS {
        int      UserID       PK
        nvarchar Name
        nvarchar Email        UK
        nvarchar PasswordHash
        varchar  NIC          UK
        varchar  Phone
        varchar  Role
        datetime CreatedAt
        bit      IsActive
        bit      NICVerified
    }

    PROPERTY {
        int      PropertyID    PK
        int      SellerID      FK
        nvarchar Title
        nvarchar Description
        nvarchar Location
        nvarchar District
        decimal  Latitude
        decimal  Longitude
        float    Size
        decimal  Price
        decimal  PricePerPerch
        varchar  DeedReference
        varchar  Status
        datetime UploadDate
    }

    PROPERTY_IMAGE {
        int      ImageID     PK
        int      PropertyID  FK
        nvarchar ImageURL
        varchar  ImageHash
        bit      IsPrimary
        datetime UploadedDate
    }

    FRAUD_CHECK {
        int      FraudCheckID       PK
        int      PropertyID         FK
        bit      PriceCheck
        bit      DuplicateCheck
        bit      NICCheck
        bit      DeedCheck
        bit      SellerHistoryCheck
        bit      LocationCheck
        bit      MissingInfoCheck
        varchar  FraudStatus
        datetime CheckDate
    }

    RISK_REPORT {
        int      ReportID      PK
        int      FraudCheckID  FK-UK
        int      RiskScore
        varchar  RiskLevel
        nvarchar Summary
        datetime GeneratedDate
    }

    SUSPICIOUS_REPORT {
        int      SuspiciousReportID PK
        int      BuyerID            FK
        int      PropertyID         FK
        nvarchar Reason
        nvarchar Description
        datetime ReportDate
        varchar  Status
    }

    NOTIFICATION {
        int      NotificationID    PK
        int      UserID            FK
        nvarchar Message
        datetime NotificationDate
        varchar  Status
        int      RelatedPropertyID FK
    }

    PODCAST {
        int      PodcastID   PK
        int      AdminID     FK
        nvarchar Title
        varchar  Language
        nvarchar Description
        nvarchar AudioURL
        datetime UploadDate
    }

    SAVED_PROPERTY {
        int      SavedPropertyID PK
        int      BuyerID         FK
        int      PropertyID      FK
        datetime SavedDate
    }

    ADMIN_ACTION {
        int      AdminActionID PK
        int      AdminID       FK
        varchar  ActionType
        int      PropertyID    FK
        int      TargetUserID  FK
        int      ReportID      FK
        nvarchar Remarks
        datetime ActionDate
    }

    PRICE_BENCHMARK {
        int      BenchmarkID         PK
        nvarchar District            UK
        decimal  MarketPricePerPerch
        datetime UpdatedDate
    }

    FRAUD_RULE_WEIGHT {
        varchar  RuleCode    PK
        nvarchar RuleName
        int      Weight
        decimal  Threshold
        bit      IsEnabled
        nvarchar Description
    }
```

## Relationships

| # | Parent | Child | Cardinality | FK column | On delete |
|---|---|---|---|---|---|
| 1 | Users | Property | 1 : M | `Property.SellerID` | NO ACTION |
| 2 | Property | PropertyImage | 1 : M | `PropertyImage.PropertyID` | CASCADE |
| 3 | Property | FraudCheck | 1 : M | `FraudCheck.PropertyID` | CASCADE |
| 4 | FraudCheck | RiskReport | **1 : 1** | `RiskReport.FraudCheckID` (UNIQUE) | CASCADE |
| 5 | Users | SuspiciousReport | 1 : M | `SuspiciousReport.BuyerID` | NO ACTION |
| 6 | Property | SuspiciousReport | 1 : M | `SuspiciousReport.PropertyID` | CASCADE |
| 7 | Users | Notification | 1 : M | `Notification.UserID` | CASCADE |
| 8 | Property | Notification | 1 : M | `Notification.RelatedPropertyID` | NO ACTION |
| 9 | Users | Podcast | 1 : M | `Podcast.AdminID` | NO ACTION |
| 10 | Users | SavedProperty | 1 : M | `SavedProperty.BuyerID` | NO ACTION |
| 11 | Property | SavedProperty | 1 : M | `SavedProperty.PropertyID` | CASCADE |
| 12 | Users | AdminAction | 1 : M | `AdminAction.AdminID` | NO ACTION |
| 13 | Users | AdminAction | 1 : M | `AdminAction.TargetUserID` | NO ACTION |
| 14 | Property | AdminAction | 1 : M | `AdminAction.PropertyID` | NO ACTION |
| 15 | SuspiciousReport | AdminAction | 1 : M | `AdminAction.ReportID` | NO ACTION |

### Why some relationships are NO ACTION

SQL Server rejects multiple cascade paths to the same table. `Users → Property`
is `NO ACTION` so that `Users → SuspiciousReport` and `Property → SuspiciousReport`
can coexist. Users are never hard-deleted anyway — accounts are suspended via
`IsActive = 0`, which preserves the fraud audit trail.

`FraudCheck` is 1 : M from `Property` rather than 1 : 1 because a seller can
correct a flagged listing and resubmit. Each analysis run is kept as history;
`vw_PropertyLatestRisk` exposes the current result.

## Normalisation

**1NF** — `PROPERTY_IMAGE` separates the repeating image group out of `PROPERTY`.

**2NF** — every table has a single-column primary key, so no partial dependencies
are possible.

**3NF** — seller details live only in `USERS` and are referenced by
`PROPERTY.SellerID`. `RISK_REPORT` stores no `PropertyID`; the link is transitive
through `FraudCheckID`.

`PROPERTY.PricePerPerch` is a **computed persisted** column derived from
`Price / Size`. It is not stored data, so it does not violate 3NF — it exists to
let the price anomaly rule use an index instead of a table scan.
