/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script      : 01_Schema.sql
  Purpose     : Creates the LandGuard database and all base tables (DDL)
  Target      : Microsoft SQL Server 2019 / 2022 Express  (LOCAL INSTANCE)
  Normal Form : 3NF (as documented in Chapter 3.1.2)
  Author      : Ladhurshan Sivasathyamoorthy - Backend & Database Developer
  Group       : Group 08 - ICBT - HD in Computing and Software Engineering
  ------------------------------------------------------------------------------
  RUN ORDER   : 01_Schema -> 02_Indexes -> 03_Views -> 04_StoredProcedures
                -> 05_SeedData -> 06_TestQueries
==============================================================================*/

/*------------------------------------------------------------------------------
  SECTION 0 : CREATE DATABASE (local instance)
------------------------------------------------------------------------------*/
USE master;
GO

IF DB_ID(N'LandGuardDB') IS NOT NULL
BEGIN
    PRINT '>> Existing LandGuardDB found - dropping it for a clean rebuild.';
    ALTER DATABASE LandGuardDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE LandGuardDB;
END
GO

CREATE DATABASE LandGuardDB;
GO

ALTER DATABASE LandGuardDB SET RECOVERY SIMPLE;
GO

USE LandGuardDB;
GO

PRINT '>> LandGuardDB created. Building schema...';
GO


/*------------------------------------------------------------------------------
  SECTION 1 : USERS      (ER entity: USER)
  ------------------------------------------------------------------------------
  NOTE: The entity is called USER in the ER diagram. USER is a reserved keyword
        in T-SQL, so the physical table is named Users. This is a naming decision
        only - the structure matches the normalised relation schema exactly.

  Extension columns (not in the ER diagram, marked below):
        NICVerified  - supports FR02 seller identity verification status
------------------------------------------------------------------------------*/
CREATE TABLE dbo.Users
(
    UserID          INT             IDENTITY(1,1)   NOT NULL,
    Name            NVARCHAR(150)                   NOT NULL,
    Email           NVARCHAR(150)                   NOT NULL,
    PasswordHash    NVARCHAR(255)                   NOT NULL,
    NIC             VARCHAR(20)                         NULL,   -- required for Seller, optional for Buyer
    Phone           VARCHAR(20)                         NULL,
    Role            VARCHAR(20)                     NOT NULL,
    CreatedAt       DATETIME2(0)                    NOT NULL,
    IsActive        BIT                             NOT NULL,
    NICVerified     BIT                             NOT NULL,   -- [extension] FR02
    -- [extension] Seller Government Identity Verification requirement.
    -- Pending/Verified/Failed - distinct from NICVerified (a plain BIT,
    -- which cannot represent three states, hence this column existing at
    -- all). Adding this column does not itself touch any NICVerified value -
    -- but the two are NOT independent at write time: usp_User_SetIdentityStatus
    -- (the sole writer of this column) and usp_Admin_VerifyNIC (the manual
    -- Admin path) both keep NICVerified in lockstep in the same UPDATE, so
    -- Verified/Pending/Failed here always agrees with NICVerified = 1/0/0 -
    -- see either procedure's own comment. NULL for Buyer/Admin - an
    -- identity check only ever applies to a Seller. See the idempotent
    -- upgrade block below for how an already-existing database's Seller
    -- rows are backfilled from their own NICVerified value.
    IdentityStatus  VARCHAR(20)                         NULL,

    CONSTRAINT PK_Users              PRIMARY KEY CLUSTERED (UserID),
    CONSTRAINT UQ_Users_Email        UNIQUE (Email),
    -- NOTE: NIC uniqueness is enforced by the FILTERED unique index
    -- UX_Users_NIC in 02_Indexes.sql, not by a UNIQUE constraint. SQL Server
    -- treats NULLs as equal inside a UNIQUE constraint, which would allow only
    -- ONE buyer without a NIC. The filtered index skips NULLs entirely.
    CONSTRAINT CK_Users_Role         CHECK (Role IN ('Buyer','Seller','Admin')),
    CONSTRAINT CK_Users_Email_Format CHECK (Email LIKE '%_@_%._%'),
    -- Sri Lankan NIC: old format 9 digits + V/X, new format 12 digits
    CONSTRAINT CK_Users_NIC_Format   CHECK
    (
        NIC IS NULL
        OR NIC LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][VvXx]'
        OR NIC LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
    ),
    -- A Seller must always supply a NIC (FR02)
    CONSTRAINT CK_Users_Seller_NIC   CHECK (Role <> 'Seller' OR NIC IS NOT NULL),
    CONSTRAINT CK_Users_IdentityStatus CHECK (IdentityStatus IS NULL OR IdentityStatus IN ('Pending','Verified','Failed'))
);
GO

ALTER TABLE dbo.Users ADD CONSTRAINT DF_Users_CreatedAt   DEFAULT (SYSDATETIME()) FOR CreatedAt;
ALTER TABLE dbo.Users ADD CONSTRAINT DF_Users_IsActive    DEFAULT (1)             FOR IsActive;
ALTER TABLE dbo.Users ADD CONSTRAINT DF_Users_NICVerified DEFAULT (0)             FOR NICVerified;
GO

/*------------------------------------------------------------------------------
  Idempotent upgrade for an ALREADY-EXISTING LandGuardDB (Seller Government
  Identity Verification requirement). Column + CHECK constraint added only if
  missing (COL_LENGTH / sys.check_constraints existence checks, the same
  pattern every other upgrade block in this script uses) - a no-op on a
  brand-new database (the base CREATE TABLE above already includes both).
  Backfills existing Seller rows ONLY, from their own existing NICVerified
  value (1 -> 'Verified', 0 -> 'Pending' - never 'Failed': there is no actual
  name/NIC mismatch evidence for a pre-existing row, and inventing one would
  contradict the "a technical/no-evidence condition must never be treated as
  a failure" principle applied elsewhere in this system). Buyer/Admin rows are
  left NULL. Guarded by "WHERE IdentityStatus IS NULL" so re-running this
  block after it has already backfilled a database is a no-op.
------------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.Users', 'IdentityStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD IdentityStatus VARCHAR(20) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_IdentityStatus')
BEGIN
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_IdentityStatus
        CHECK (IdentityStatus IS NULL OR IdentityStatus IN ('Pending','Verified','Failed'));
END
GO

UPDATE dbo.Users
SET IdentityStatus = CASE WHEN NICVerified = 1 THEN 'Verified' ELSE 'Pending' END
WHERE Role = 'Seller' AND IdentityStatus IS NULL;
GO


/*------------------------------------------------------------------------------
  SECTION 2 : PRICE BENCHMARK   [extension table]
  ------------------------------------------------------------------------------
  Reference data used by fraud CHECK 1 (Price Anomaly). Holds the accepted
  market rate per perch for each district so a submitted price can be compared
  against a known baseline even when there are no comparable live listings.
------------------------------------------------------------------------------*/
CREATE TABLE dbo.PriceBenchmark
(
    BenchmarkID         INT             IDENTITY(1,1)   NOT NULL,
    District            NVARCHAR(100)                   NOT NULL,
    MarketPricePerPerch DECIMAL(14,2)                   NOT NULL,
    UpdatedDate         DATETIME2(0)                    NOT NULL,

    CONSTRAINT PK_PriceBenchmark          PRIMARY KEY CLUSTERED (BenchmarkID),
    CONSTRAINT UQ_PriceBenchmark_District UNIQUE (District),
    CONSTRAINT CK_PriceBenchmark_Price    CHECK (MarketPricePerPerch > 0)
);
GO

ALTER TABLE dbo.PriceBenchmark ADD CONSTRAINT DF_PriceBenchmark_Updated
    DEFAULT (SYSDATETIME()) FOR UpdatedDate;
GO


/*------------------------------------------------------------------------------
  SECTION 3 : PROPERTY
  ------------------------------------------------------------------------------
  Extension columns:
        District        - normalised district used for price benchmarking
        Latitude/Longitude - filled from the Nominatim API (fraud CHECK 6)
        PricePerPerch   - PERSISTED computed column, used by the price anomaly rule
------------------------------------------------------------------------------*/
CREATE TABLE dbo.Property
(
    PropertyID      INT             IDENTITY(1,1)   NOT NULL,
    SellerID        INT                             NOT NULL,
    Title           NVARCHAR(200)                   NOT NULL,
    Description     NVARCHAR(MAX)                       NULL,
    Location        NVARCHAR(255)                   NOT NULL,
    District        NVARCHAR(100)                       NULL,   -- [extension]
    Latitude        DECIMAL(9,6)                        NULL,   -- [extension] Nominatim
    Longitude       DECIMAL(9,6)                        NULL,   -- [extension] Nominatim
    Size            FLOAT                           NOT NULL,   -- land size in perches
    Price           DECIMAL(14,2)                   NOT NULL,
    DeedReference   VARCHAR(100)                        NULL,
    -- Explicit deed-owner fields (Owner Name / Owner NIC / Owner Address
    -- requirement). Nullable at the schema level - deliberately NOT a
    -- NOT NULL/CHECK constraint, so an idempotent ALTER TABLE ADD on an
    -- already-existing LandGuardDB never needs to fabricate a value for
    -- rows created before this requirement existed (see the idempotent
    -- upgrade block below for the full reasoning). "Mandatory" for every
    -- NEW listing is enforced instead at the Application layer
    -- (CreatePropertyRequestValidator) and inside usp_Property_Create
    -- itself (RAISERROR guard, the same defence-in-depth style
    -- usp_Property_Create already uses for "Seller not found").
    OwnerName       NVARCHAR(150)                       NULL,
    OwnerNIC        VARCHAR(20)                         NULL,
    OwnerAddress    NVARCHAR(255)                       NULL,
    -- [extension] Global Duplicate-Property Prevention requirement. The
    -- authoritative GovernmentLandRecordDto.PropertyReference this
    -- PropertyID last resolved to during Government Registry verification -
    -- NULL until a verification run actually resolves one. Used only to
    -- detect a second PropertyID resolving to the same government parcel
    -- (usp_Property_FindByGovernmentPropertyReference); never displayed to
    -- a Buyer, never used as the deed-duplicate identifier itself (that
    -- remains DeedReference - see usp_Property_Create's own header comment).
    GovernmentPropertyReference NVARCHAR(50)            NULL,
    Status          VARCHAR(20)                     NOT NULL,
    UploadDate      DATETIME2(0)                    NOT NULL,

    PricePerPerch AS
        (CASE WHEN Size > 0 THEN CAST(Price / CAST(Size AS DECIMAL(14,4)) AS DECIMAL(14,2)) END) PERSISTED,

    CONSTRAINT PK_Property            PRIMARY KEY CLUSTERED (PropertyID),
    CONSTRAINT FK_Property_Seller     FOREIGN KEY (SellerID)
        REFERENCES dbo.Users (UserID) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT CK_Property_Status     CHECK (Status IN ('Pending','Approved','Flagged','Rejected','Withdrawn','Disapproved')),
    CONSTRAINT CK_Property_Price      CHECK (Price > 0),
    CONSTRAINT CK_Property_Size       CHECK (Size > 0)
);
GO

ALTER TABLE dbo.Property ADD CONSTRAINT DF_Property_Status     DEFAULT ('Pending')      FOR Status;
ALTER TABLE dbo.Property ADD CONSTRAINT DF_Property_UploadDate DEFAULT (SYSDATETIME())  FOR UploadDate;
GO

/*------------------------------------------------------------------------------
  Idempotent upgrade for an ALREADY-EXISTING LandGuardDB (Phase F, Property
  Withdrawal). dbo.Property's CREATE TABLE above is not guarded by
  IF OBJECT_ID(...) IS NULL, so on a database that already has this table the
  block above is skipped entirely and CK_Property_Status is never touched.
  This block re-adds the constraint with 'Withdrawn' included ONLY if it is
  missing, so it is safe to run against both a brand-new database (no-op,
  constraint already created above with 'Withdrawn' present) and an existing
  one (actually performs the upgrade). Safe to re-run any number of times.
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Property_Status'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%Withdrawn%'
)
BEGIN
    ALTER TABLE dbo.Property DROP CONSTRAINT CK_Property_Status;
    ALTER TABLE dbo.Property ADD CONSTRAINT CK_Property_Status
        CHECK (Status IN ('Pending','Approved','Flagged','Rejected','Withdrawn'));
END
GO

/*------------------------------------------------------------------------------
  Idempotent upgrade for an ALREADY-EXISTING LandGuardDB (Mandatory Deed /
  Form-vs-Deed Verification requirement). Adds 'Disapproved' - a SYSTEM-
  AUTOMATED outcome (Form-vs-Deed mismatch, or any Government Registry
  mismatch other than a standalone price anomaly), distinct from 'Rejected'
  (which stays exactly what its own PropertyStatus.cs doc comment already
  says: a manual Admin decision). Same guarded DROP/ADD pattern as the
  'Withdrawn' upgrade above - a no-op on a brand-new database (the base
  CREATE TABLE above already includes 'Disapproved'), an actual upgrade on
  an existing one, safe to re-run any number of times.
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Property_Status'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%Disapproved%'
)
BEGIN
    ALTER TABLE dbo.Property DROP CONSTRAINT CK_Property_Status;
    ALTER TABLE dbo.Property ADD CONSTRAINT CK_Property_Status
        CHECK (Status IN ('Pending','Approved','Flagged','Rejected','Withdrawn','Disapproved'));
END
GO

/*------------------------------------------------------------------------------
  Idempotent upgrade for an ALREADY-EXISTING LandGuardDB (Owner Name / Owner
  NIC / Owner Address requirement). Each column is added only if missing -
  COL_LENGTH(...) IS NULL is the standard idempotent-column-add check, the
  same role OBJECT_DEFINITION(...) NOT LIKE '...' plays for the CHECK
  constraint upgrades above. No default/backfill value, so this never writes
  to any existing Property row - old rows simply have NULL here (accurate:
  they never captured this data), exactly like every other pre-existing
  optional column. Safe to re-run any number of times.
------------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.Property', 'OwnerName') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD OwnerName NVARCHAR(150) NULL;
END
GO

IF COL_LENGTH('dbo.Property', 'OwnerNIC') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD OwnerNIC VARCHAR(20) NULL;
END
GO

IF COL_LENGTH('dbo.Property', 'OwnerAddress') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD OwnerAddress NVARCHAR(255) NULL;
END
GO

/*------------------------------------------------------------------------------
  Idempotent upgrade for an ALREADY-EXISTING LandGuardDB (Global
  Duplicate-Property Prevention requirement). No default/backfill - a
  verification run is what populates this, never a migration.
------------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.Property', 'GovernmentPropertyReference') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD GovernmentPropertyReference NVARCHAR(50) NULL;
END
GO


/*------------------------------------------------------------------------------
  SECTION 4 : PROPERTY_IMAGE
  ------------------------------------------------------------------------------
  ImageHash stores a perceptual/SHA fingerprint produced by the API layer.
  It is the input to fraud CHECK 2 (Duplicate Image).
------------------------------------------------------------------------------*/
CREATE TABLE dbo.PropertyImage
(
    ImageID         INT             IDENTITY(1,1)   NOT NULL,
    PropertyID      INT                             NOT NULL,
    ImageURL        NVARCHAR(500)                   NOT NULL,
    ImageHash       VARCHAR(255)                        NULL,
    IsPrimary       BIT                             NOT NULL,   -- [extension] thumbnail flag
    UploadedDate    DATETIME2(0)                    NOT NULL,

    CONSTRAINT PK_PropertyImage          PRIMARY KEY CLUSTERED (ImageID),
    CONSTRAINT FK_PropertyImage_Property FOREIGN KEY (PropertyID)
        REFERENCES dbo.Property (PropertyID) ON DELETE CASCADE
);
GO

ALTER TABLE dbo.PropertyImage ADD CONSTRAINT DF_PropertyImage_IsPrimary DEFAULT (0)            FOR IsPrimary;
ALTER TABLE dbo.PropertyImage ADD CONSTRAINT DF_PropertyImage_Uploaded  DEFAULT (SYSDATETIME()) FOR UploadedDate;
GO


/*------------------------------------------------------------------------------
  SECTION 5 : FRAUD_CHECK
  ------------------------------------------------------------------------------
  Stores the outcome of the 7 independent rule checks for one analysis run.
  CONVENTION:  1 (TRUE)  = the rule FIRED, i.e. a fraud indicator was DETECTED
               0 (FALSE) = the rule passed cleanly
  A property may be analysed more than once (seller corrects and resubmits),
  so this table keeps full history; the latest row is the current result.
------------------------------------------------------------------------------*/
CREATE TABLE dbo.FraudCheck
(
    FraudCheckID        INT         IDENTITY(1,1)   NOT NULL,
    PropertyID          INT                         NOT NULL,
    PriceCheck          BIT                         NOT NULL,   -- Check 1: price anomaly
    DuplicateCheck      BIT                         NOT NULL,   -- Check 2: duplicate image
    NICCheck            BIT                         NOT NULL,   -- Check 3: seller NIC verification
    DeedCheck           BIT                         NOT NULL,   -- Check 4: deed reference duplicate
    SellerHistoryCheck  BIT                         NOT NULL,   -- Check 5: seller history
    LocationCheck       BIT                         NOT NULL,   -- Check 6: location validation
    MissingInfoCheck    BIT                         NOT NULL,   -- Check 7: missing information
    FraudStatus         VARCHAR(20)                 NOT NULL,
    CheckDate           DATETIME2(0)                NOT NULL,

    CONSTRAINT PK_FraudCheck            PRIMARY KEY CLUSTERED (FraudCheckID),
    CONSTRAINT FK_FraudCheck_Property   FOREIGN KEY (PropertyID)
        REFERENCES dbo.Property (PropertyID) ON DELETE CASCADE,
    CONSTRAINT CK_FraudCheck_Status     CHECK (FraudStatus IN ('Clean','Suspicious','Fraudulent'))
);
GO

ALTER TABLE dbo.FraudCheck ADD CONSTRAINT DF_FraudCheck_CheckDate DEFAULT (SYSDATETIME()) FOR CheckDate;
GO


/*------------------------------------------------------------------------------
  SECTION 6 : RISK_REPORT
  ------------------------------------------------------------------------------
  The 8th point of the fraud engine: the combined risk score.
  1:1 with FRAUD_CHECK (enforced by the UNIQUE constraint).
  PropertyID is deliberately NOT stored here - it is reachable transitively
  through FraudCheckID, and repeating it would break 3NF (see Chapter 3.1.2).
------------------------------------------------------------------------------*/
CREATE TABLE dbo.RiskReport
(
    ReportID        INT             IDENTITY(1,1)   NOT NULL,
    FraudCheckID    INT                             NOT NULL,
    RiskScore       INT                             NOT NULL,
    RiskLevel       VARCHAR(20)                     NOT NULL,
    Summary         NVARCHAR(MAX)                       NULL,
    GeneratedDate   DATETIME2(0)                    NOT NULL,

    CONSTRAINT PK_RiskReport             PRIMARY KEY CLUSTERED (ReportID),
    CONSTRAINT UQ_RiskReport_FraudCheck  UNIQUE (FraudCheckID),
    CONSTRAINT FK_RiskReport_FraudCheck  FOREIGN KEY (FraudCheckID)
        REFERENCES dbo.FraudCheck (FraudCheckID) ON DELETE CASCADE,
    CONSTRAINT CK_RiskReport_Score       CHECK (RiskScore BETWEEN 0 AND 100),
    CONSTRAINT CK_RiskReport_Level       CHECK (RiskLevel IN ('Low','Medium','High')),
    -- Enforces FR05 banding: Low 0-40, Medium 41-70, High 71-100
    CONSTRAINT CK_RiskReport_Banding     CHECK
    (
        (RiskLevel = 'Low'    AND RiskScore BETWEEN  0 AND  40) OR
        (RiskLevel = 'Medium' AND RiskScore BETWEEN 41 AND  70) OR
        (RiskLevel = 'High'   AND RiskScore BETWEEN 71 AND 100)
    )
);
GO

ALTER TABLE dbo.RiskReport ADD CONSTRAINT DF_RiskReport_Generated DEFAULT (SYSDATETIME()) FOR GeneratedDate;
GO


/*------------------------------------------------------------------------------
  SECTION 7 : SUSPICIOUS_REPORT   (FR12 - buyer reports a listing)
------------------------------------------------------------------------------*/
CREATE TABLE dbo.SuspiciousReport
(
    SuspiciousReportID  INT             IDENTITY(1,1)   NOT NULL,
    BuyerID             INT                             NOT NULL,
    PropertyID          INT                             NOT NULL,
    Reason              NVARCHAR(255)                   NOT NULL,
    Description         NVARCHAR(MAX)                       NULL,
    ReportDate          DATETIME2(0)                    NOT NULL,
    Status              VARCHAR(20)                     NOT NULL,

    CONSTRAINT PK_SuspiciousReport          PRIMARY KEY CLUSTERED (SuspiciousReportID),
    CONSTRAINT FK_SuspiciousReport_Buyer    FOREIGN KEY (BuyerID)
        REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,
    CONSTRAINT FK_SuspiciousReport_Property FOREIGN KEY (PropertyID)
        REFERENCES dbo.Property (PropertyID) ON DELETE CASCADE,
    CONSTRAINT CK_SuspiciousReport_Status   CHECK (Status IN ('Open','Under Review','Resolved')),
    -- Same buyer cannot file the same reason twice on the same property
    CONSTRAINT UQ_SuspiciousReport_Once     UNIQUE (BuyerID, PropertyID, Reason)
);
GO

ALTER TABLE dbo.SuspiciousReport ADD CONSTRAINT DF_SuspiciousReport_Date   DEFAULT (SYSDATETIME()) FOR ReportDate;
ALTER TABLE dbo.SuspiciousReport ADD CONSTRAINT DF_SuspiciousReport_Status DEFAULT ('Open')        FOR Status;
GO


/*------------------------------------------------------------------------------
  SECTION 8 : NOTIFICATION   (FR07 - fraud alerts and notifications)
------------------------------------------------------------------------------*/
CREATE TABLE dbo.Notification
(
    NotificationID      INT             IDENTITY(1,1)   NOT NULL,
    UserID              INT                             NOT NULL,
    Message             NVARCHAR(500)                   NOT NULL,
    NotificationDate    DATETIME2(0)                    NOT NULL,
    Status              VARCHAR(20)                     NOT NULL,
    RelatedPropertyID   INT                                 NULL,   -- [extension] deep link

    CONSTRAINT PK_Notification          PRIMARY KEY CLUSTERED (NotificationID),
    CONSTRAINT FK_Notification_User     FOREIGN KEY (UserID)
        REFERENCES dbo.Users (UserID) ON DELETE CASCADE,
    CONSTRAINT FK_Notification_Property FOREIGN KEY (RelatedPropertyID)
        REFERENCES dbo.Property (PropertyID) ON DELETE NO ACTION,
    CONSTRAINT CK_Notification_Status   CHECK (Status IN ('Read','Unread'))
);
GO

ALTER TABLE dbo.Notification ADD CONSTRAINT DF_Notification_Date   DEFAULT (SYSDATETIME()) FOR NotificationDate;
ALTER TABLE dbo.Notification ADD CONSTRAINT DF_Notification_Status DEFAULT ('Unread')      FOR Status;
GO


/*------------------------------------------------------------------------------
  SECTION 9 : PODCAST   (FR11 - multilingual fraud awareness content)
------------------------------------------------------------------------------*/
CREATE TABLE dbo.Podcast
(
    PodcastID       INT             IDENTITY(1,1)   NOT NULL,
    AdminID         INT                             NOT NULL,
    Title           NVARCHAR(200)                   NOT NULL,
    Language        VARCHAR(50)                     NOT NULL,
    Description     NVARCHAR(MAX)                       NULL,
    AudioURL        NVARCHAR(500)                   NOT NULL,
    UploadDate      DATETIME2(0)                    NOT NULL,

    CONSTRAINT PK_Podcast           PRIMARY KEY CLUSTERED (PodcastID),
    CONSTRAINT FK_Podcast_Admin     FOREIGN KEY (AdminID)
        REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,
    CONSTRAINT CK_Podcast_Language  CHECK (Language IN ('English','Sinhala','Tamil'))
);
GO

ALTER TABLE dbo.Podcast ADD CONSTRAINT DF_Podcast_UploadDate DEFAULT (SYSDATETIME()) FOR UploadDate;
GO


/*------------------------------------------------------------------------------
  SECTION 10 : SAVED_PROPERTY   [extension table - FR07]
  Buyer dashboard "saved properties" feature.
------------------------------------------------------------------------------*/
CREATE TABLE dbo.SavedProperty
(
    SavedPropertyID INT             IDENTITY(1,1)   NOT NULL,
    BuyerID         INT                             NOT NULL,
    PropertyID      INT                             NOT NULL,
    SavedDate       DATETIME2(0)                    NOT NULL,

    CONSTRAINT PK_SavedProperty          PRIMARY KEY CLUSTERED (SavedPropertyID),
    CONSTRAINT FK_SavedProperty_Buyer    FOREIGN KEY (BuyerID)
        REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,
    CONSTRAINT FK_SavedProperty_Property FOREIGN KEY (PropertyID)
        REFERENCES dbo.Property (PropertyID) ON DELETE CASCADE,
    CONSTRAINT UQ_SavedProperty_Pair     UNIQUE (BuyerID, PropertyID)
);
GO

ALTER TABLE dbo.SavedProperty ADD CONSTRAINT DF_SavedProperty_Date DEFAULT (SYSDATETIME()) FOR SavedDate;
GO


/*------------------------------------------------------------------------------
  SECTION 11 : ADMIN_ACTION   [extension table - FR09]
  Audit trail of every administrative decision. Required for NFR02 (security /
  accountability) and for the admin statistics on the dashboard.
------------------------------------------------------------------------------*/
CREATE TABLE dbo.AdminAction
(
    AdminActionID   INT             IDENTITY(1,1)   NOT NULL,
    AdminID         INT                             NOT NULL,
    ActionType      VARCHAR(30)                     NOT NULL,
    PropertyID      INT                                 NULL,
    TargetUserID    INT                                 NULL,
    ReportID        INT                                 NULL,   -- SuspiciousReportID
    Remarks         NVARCHAR(500)                       NULL,
    ActionDate      DATETIME2(0)                    NOT NULL,

    CONSTRAINT PK_AdminAction           PRIMARY KEY CLUSTERED (AdminActionID),
    CONSTRAINT FK_AdminAction_Admin     FOREIGN KEY (AdminID)
        REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,
    CONSTRAINT FK_AdminAction_Property  FOREIGN KEY (PropertyID)
        REFERENCES dbo.Property (PropertyID) ON DELETE NO ACTION,
    CONSTRAINT FK_AdminAction_TargetUsr FOREIGN KEY (TargetUserID)
        REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,
    CONSTRAINT FK_AdminAction_Report    FOREIGN KEY (ReportID)
        REFERENCES dbo.SuspiciousReport (SuspiciousReportID) ON DELETE NO ACTION,
    CONSTRAINT CK_AdminAction_Type      CHECK (ActionType IN
        ('ApproveListing','RejectListing','FlagListing','SuspendUser',
         'ReactivateUser','VerifyNIC','ResolveReport','RemoveListing'))
);
GO

ALTER TABLE dbo.AdminAction ADD CONSTRAINT DF_AdminAction_Date DEFAULT (SYSDATETIME()) FOR ActionDate;
GO


/*------------------------------------------------------------------------------
  SECTION 12 : FRAUD RULE WEIGHTS   [extension - configurable rule engine]
  ------------------------------------------------------------------------------
  Holds the penalty weight of each of the 7 rules. Weights total 100 so the
  combined score always falls inside the FR05 range of 0-100.
  Storing them in a table (rather than hard-coding) directly mitigates the
  first Risk Analysis item in Chapter 3.3: "review and adjust rule thresholds
  periodically" - the threshold can be tuned without redeploying the API.
------------------------------------------------------------------------------*/
CREATE TABLE dbo.FraudRuleWeight
(
    RuleCode        VARCHAR(30)     NOT NULL,
    RuleName        NVARCHAR(100)   NOT NULL,
    Weight          INT             NOT NULL,
    Threshold       DECIMAL(9,4)        NULL,   -- rule-specific tuning value
    IsEnabled       BIT             NOT NULL,
    Description     NVARCHAR(400)       NULL,

    CONSTRAINT PK_FraudRuleWeight       PRIMARY KEY CLUSTERED (RuleCode),
    CONSTRAINT CK_FraudRuleWeight_Wt    CHECK (Weight BETWEEN 0 AND 100)
);
GO

ALTER TABLE dbo.FraudRuleWeight ADD CONSTRAINT DF_FraudRuleWeight_Enabled DEFAULT (1) FOR IsEnabled;
GO

INSERT INTO dbo.FraudRuleWeight (RuleCode, RuleName, Weight, Threshold, Description) VALUES
 ('NIC_VERIFICATION', N'Seller NIC Verification',      20, NULL,
  N'Seller NIC is missing, malformed, unverified, or shared with another account.'),
 ('DEED_DUPLICATE',   N'Deed Reference Duplicate',     20, NULL,
  N'The same deed reference already appears on another active listing.'),
 ('IMAGE_DUPLICATE',  N'Duplicate Image',              15, NULL,
  N'One or more image hashes already exist on a different property.'),
 ('PRICE_ANOMALY',    N'Price Anomaly',                15, 0.4000,
  N'Price per perch is more than 40% below the district market benchmark.'),
 ('SELLER_HISTORY',   N'Seller History',               12, 2.0000,
  N'Seller has 2 or more previously rejected listings or upheld suspicious reports.'),
 ('LOCATION_INVALID', N'Location Validation',          10, NULL,
  N'Location could not be resolved to valid coordinates by the Nominatim API.'),
 ('MISSING_INFO',     N'Missing Information',           8, 1.0000,
  N'Mandatory listing details (description, deed, images, size or contact) are absent.');
GO

PRINT '>> Schema created: 12 tables (8 from the ER diagram + 4 supporting tables).';
GO
