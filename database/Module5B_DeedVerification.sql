/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : Module5B_DeedVerification.sql
  Purpose : Adds persistence for the Government Registry module's Phase 5A
            classification result (GovernmentDeedFraudDetectionResult) -
            three new tables (DeedVerification, DeedVerificationField,
            DeedVerificationReason) and four new stored procedures. Nothing
            about this module previously had a database presence at all:
            GovernmentDeedComparisonReport and GovernmentDeedFraudDetectionResult
            are both explicitly "never persisted" per their own doc
            comments - this script is what changes that.

  Context: this checkout does not contain the canonical Database/Scripts
  folder (it lives in the database owner's own checkout) - same situation
  Module3_ChangePassword.sql and Module5A_FraudHistory.sql already
  documented. Adding a new, additive script here means no existing table or
  procedure is ever touched. Please fold these tables/procedures into the
  canonical package (new table-creation section + Section D/F-style
  procedures) the next time that repository is updated.

  Nature of the change : ADDITIVE ONLY.
    - Three new tables. No ALTER TABLE against any existing table, no new
      column on Property/Users/FraudCheck/RiskReport/anything else.
    - Four new CREATE OR ALTER PROCEDUREs. No existing procedure is
      modified.
    - No DROP of anything.
    - Deliberately NO usp_DeedVerification_Update and NO
      usp_DeedVerification_Delete - these three tables are APPEND-ONLY,
      the same "one row per run, never edited" shape dbo.FraudCheck already
      established for the numeric fraud engine (see FraudCheck's own doc
      comment: "A property can have several rows... this table keeps full
      history"). A corrected/new verification inserts a new
      DeedVerification row; nothing here ever UPDATEs or DELETEs a past
      one.

  Safety / re-runnability:
    - Table creation is guarded with IF OBJECT_ID(...) IS NULL, so running
      this script again after the tables already exist is a no-op for the
      CREATE TABLE statements.
    - Index creation is guarded with a sys.indexes existence check for the
      same reason.
    - Every procedure uses CREATE OR ALTER PROCEDURE (idempotent re-run),
      matching Module3_ChangePassword.sql/Module5A_FraudHistory.sql exactly.

  Foreign keys:
    - DeedVerification.PropertyID -> Property.PropertyID: ON DELETE NO
      ACTION (not CASCADE, unlike FK_FraudCheck_Property). Verification
      history is audit/history data; a property deletion must never
      silently cascade-delete the evidence of what was verified about it.
    - DeedVerification.SubmittedByUserID -> Users.UserID: ON DELETE NO
      ACTION, matching FK_Property_Seller exactly ("Users are suspended,
      never deleted").
    - DeedVerificationField/DeedVerificationReason -> DeedVerification: ON
      DELETE CASCADE, matching FK_RiskReport_FraudCheck exactly - this
      child evidence/reason data has no meaning without its parent
      verification row. This FK cascade is a database-integrity mechanism
      only; there is still no DELETE procedure for normal application use,
      so nothing in the API/service layer can trigger it directly.
    - No FK to a "GovernmentRecord" table exists or is added -
      GovernmentRecordID is a plain business-key string (e.g. "GR-000001"),
      because the government registry is not itself a database table in
      this project (see DummyGovernmentRegistryService - it is a fully
      in-memory stand-in for a future external government API).

  VerificationStatus / Reason string vocabularies:
    Both CHECK constraints below enumerate the exact string values
    LandGuard.Domain.Enums.DeedVerificationStatus and
    LandGuard.Domain.Enums.DeedFraudReason serialize to via their EF Core
    HasConversion<string>() mapping (each enum member's own C# name,
    unchanged) - inspected directly from those two enum files before
    writing this script, not guessed. FieldName's CHECK constraint likewise
    enumerates the exact 9 FieldName values
    LandGuard.Application.Services.DeedFieldComparer.Compare produces
    (inspected directly from that file), and GovernmentRecordStatus's CHECK
    constraint enumerates the exact 3 values
    GovernmentLandRecordDto.Status's own doc comment documents as possible
    ("Active" | "Cancelled" | "Suspended").
  Author  : LandGuard Government Registry module, Phase 5B (Deed Verification Persistence)
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  dbo.DeedVerification
  One row per government deed verification run - the parent row, mirroring
  dbo.FraudCheck's "one row per analysis run" shape for this independent,
  evidence-based verdict (never written into FraudCheck itself - see
  GovernmentDeedFraudDetectionService's own doc comment for why the two
  systems stay separate).
------------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.DeedVerification', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeedVerification
    (
        DeedVerificationID      INT             IDENTITY(1,1) NOT NULL,
        PropertyID               INT             NOT NULL,
        SubmittedByUserID        INT             NOT NULL,
        GovernmentRecordID       VARCHAR(20)     NULL,
        GovernmentRecordStatus   VARCHAR(20)     NULL,
        VerificationStatus       VARCHAR(30)     NOT NULL,
        Summary                  NVARCHAR(MAX)   NULL,
        SellerDocumentReference  VARCHAR(255)    NULL,
        VerifiedDate             DATETIME2(0)    NOT NULL,

        CONSTRAINT PK_DeedVerification PRIMARY KEY CLUSTERED (DeedVerificationID),

        CONSTRAINT FK_DeedVerification_Property FOREIGN KEY (PropertyID)
            REFERENCES dbo.Property (PropertyID) ON DELETE NO ACTION,

        CONSTRAINT FK_DeedVerification_Users FOREIGN KEY (SubmittedByUserID)
            REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,

        -- Matches LandGuard.Domain.Enums.DeedVerificationStatus's 5 exact
        -- member names (Phase 5A) - see this script's header comment.
        CONSTRAINT CK_DeedVerification_Status CHECK (VerificationStatus IN
            (N'Verified', N'Fraudulent', N'PriceAnomaly', N'Unverified', N'UnverifiedCancelled')),

        -- Matches GovernmentLandRecordDto.Status's own documented 3 values.
        -- NULL is allowed - no government record could be resolved at all.
        CONSTRAINT CK_DeedVerification_GovernmentRecordStatus CHECK (GovernmentRecordStatus IS NULL OR GovernmentRecordStatus IN
            (N'Active', N'Cancelled', N'Suspended'))
    );

    PRINT '>> dbo.DeedVerification created (Module 5B).';
END
GO

-- History-by-property lookup (usp_DeedVerification_GetHistory relies on
-- this shape) - mirrors IX_FraudCheck_Property_Date exactly, with an
-- explicit DESC key order since verification history is always read
-- newest-first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeedVerification_Property_Date' AND object_id = OBJECT_ID('dbo.DeedVerification'))
BEGIN
    CREATE INDEX IX_DeedVerification_Property_Date
        ON dbo.DeedVerification (PropertyID, VerifiedDate DESC);

    PRINT '>> IX_DeedVerification_Property_Date created (Module 5B).';
END
GO

/*------------------------------------------------------------------------------
  dbo.DeedVerificationField
  One row per compared field - persists exactly what DeedFieldComparisonResult
  already computed (FieldName/GovernmentValue/SellerValue/Match/Message),
  normalized rather than JSON, matching this schema's existing preference
  for child rows (FraudCheck/RiskReport, Property/PropertyImage,
  SuspiciousReport/AdminAction) over blob columns.
------------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.DeedVerificationField', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeedVerificationField
    (
        DeedVerificationFieldID INT             IDENTITY(1,1) NOT NULL,
        DeedVerificationID      INT             NOT NULL,
        FieldName               VARCHAR(30)     NOT NULL,
        GovernmentValue         NVARCHAR(255)   NULL,
        SellerValue              NVARCHAR(255)   NULL,
        IsMatch                  BIT             NOT NULL,
        Message                  NVARCHAR(400)   NULL,

        CONSTRAINT PK_DeedVerificationField PRIMARY KEY CLUSTERED (DeedVerificationFieldID),

        CONSTRAINT FK_DeedVerificationField_DeedVerification FOREIGN KEY (DeedVerificationID)
            REFERENCES dbo.DeedVerification (DeedVerificationID) ON DELETE CASCADE,

        -- Matches the exact 9 FieldName values
        -- LandGuard.Application.Services.DeedFieldComparer.Compare
        -- produces - see this script's header comment.
        CONSTRAINT CK_DeedVerificationField_FieldName CHECK (FieldName IN
            (N'NIC', N'OwnerName', N'DeedNumber', N'PropertyReference', N'LandSize',
             N'District', N'Address', N'Price', N'RegistrationDate'))
    );

    PRINT '>> dbo.DeedVerificationField created (Module 5B).';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeedVerificationField_VerificationID' AND object_id = OBJECT_ID('dbo.DeedVerificationField'))
BEGIN
    CREATE INDEX IX_DeedVerificationField_VerificationID
        ON dbo.DeedVerificationField (DeedVerificationID);

    PRINT '>> IX_DeedVerificationField_VerificationID created (Module 5B).';
END
GO

/*------------------------------------------------------------------------------
  dbo.DeedVerificationReason
  One row per DeedFraudReason contributing to a run's VerificationStatus - a
  run can have more than one (e.g. a NIC mismatch and a Deed Number mismatch
  together also carry MultipleFieldMismatch).
------------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.DeedVerificationReason', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeedVerificationReason
    (
        DeedVerificationReasonID INT           IDENTITY(1,1) NOT NULL,
        DeedVerificationID       INT           NOT NULL,
        Reason                    VARCHAR(50)   NOT NULL,

        CONSTRAINT PK_DeedVerificationReason PRIMARY KEY CLUSTERED (DeedVerificationReasonID),

        CONSTRAINT FK_DeedVerificationReason_DeedVerification FOREIGN KEY (DeedVerificationID)
            REFERENCES dbo.DeedVerification (DeedVerificationID) ON DELETE CASCADE,

        -- Matches LandGuard.Domain.Enums.DeedFraudReason's 13 exact member
        -- names (Phase 5A) - see this script's header comment.
        CONSTRAINT CK_DeedVerificationReason_Reason CHECK (Reason IN
            (N'NicMismatch', N'OwnerNameMismatch', N'DeedNumberMismatch', N'PropertyReferenceMismatch',
             N'LandSizeMismatch', N'DistrictMismatch', N'AddressMismatch', N'RegistrationDateMismatch',
             N'MultipleFieldMismatch', N'PriceAnomalyDetected', N'GovernmentRecordNotFound',
             N'GovernmentRecordCancelled', N'GovernmentDocumentUnavailable'))
    );

    PRINT '>> dbo.DeedVerificationReason created (Module 5B).';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeedVerificationReason_VerificationID' AND object_id = OBJECT_ID('dbo.DeedVerificationReason'))
BEGIN
    CREATE INDEX IX_DeedVerificationReason_VerificationID
        ON dbo.DeedVerificationReason (DeedVerificationID);

    PRINT '>> IX_DeedVerificationReason_VerificationID created (Module 5B).';
END
GO

/*------------------------------------------------------------------------------
  usp_DeedVerification_Create   ->  the parent-row write
  Inserts one DeedVerification row and returns its generated ID via OUTPUT,
  the same dual convention usp_Property_Create established
  (@NewPropertyID OUTPUT). @VerifiedDate is a required parameter, not
  defaulted by SQL (e.g. via GETUTCDATE()) - the caller
  (GovernmentDeedVerificationService) already computed this timestamp once
  (GovernmentDeedComparisonReport.GeneratedDate), so this procedure reuses
  it rather than computing a second, possibly-drifted one.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_DeedVerification_Create
    @PropertyID              INT,
    @SubmittedByUserID       INT,
    @GovernmentRecordID      VARCHAR(20)   = NULL,
    @GovernmentRecordStatus  VARCHAR(20)   = NULL,
    @VerificationStatus      VARCHAR(30),
    @Summary                 NVARCHAR(MAX) = NULL,
    @SellerDocumentReference VARCHAR(255)  = NULL,
    @VerifiedDate            DATETIME2(0),
    @NewDeedVerificationID   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @SubmittedByUserID)
    BEGIN
        RAISERROR (N'Submitting user not found.', 16, 1);
        RETURN -1;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.DeedVerification
            (PropertyID, SubmittedByUserID, GovernmentRecordID, GovernmentRecordStatus,
             VerificationStatus, Summary, SellerDocumentReference, VerifiedDate)
        VALUES
            (@PropertyID, @SubmittedByUserID, @GovernmentRecordID, @GovernmentRecordStatus,
             @VerificationStatus, @Summary, @SellerDocumentReference, @VerifiedDate);

        SET @NewDeedVerificationID = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    -- The freshly-created row, for a caller that wants it without a second
    -- round trip (mirrors usp_Property_Create's own final SELECT). Phase
    -- 5B's own Dapper wrapper does not read this result set (only the
    -- OUTPUT parameter) - kept for forward compatibility and convention
    -- consistency with usp_Property_Create.
    SELECT
        DeedVerificationID, PropertyID, SubmittedByUserID, GovernmentRecordID, GovernmentRecordStatus,
        VerificationStatus, Summary, SellerDocumentReference, VerifiedDate
    FROM dbo.DeedVerification
    WHERE DeedVerificationID = @NewDeedVerificationID;

    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_DeedVerificationField_Add   ->  one field-evidence child row
  Called once per DeedFieldComparisonResult by
  GovernmentDeedVerificationService, inside the same ambient transaction as
  usp_DeedVerification_Create (see that service's own doc comment for how).
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_DeedVerificationField_Add
    @DeedVerificationID INT,
    @FieldName          VARCHAR(30),
    @GovernmentValue    NVARCHAR(255) = NULL,
    @SellerValue        NVARCHAR(255) = NULL,
    @IsMatch             BIT,
    @Message             NVARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.DeedVerification WHERE DeedVerificationID = @DeedVerificationID)
    BEGIN
        RAISERROR (N'Deed verification run not found.', 16, 1);
        RETURN -1;
    END

    INSERT INTO dbo.DeedVerificationField
        (DeedVerificationID, FieldName, GovernmentValue, SellerValue, IsMatch, Message)
    VALUES
        (@DeedVerificationID, @FieldName, @GovernmentValue, @SellerValue, @IsMatch, @Message);

    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_DeedVerificationReason_Add   ->  one reason child row
  Called once per DeedFraudReason by GovernmentDeedVerificationService,
  inside the same ambient transaction as usp_DeedVerification_Create.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_DeedVerificationReason_Add
    @DeedVerificationID INT,
    @Reason             VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.DeedVerification WHERE DeedVerificationID = @DeedVerificationID)
    BEGIN
        RAISERROR (N'Deed verification run not found.', 16, 1);
        RETURN -1;
    END

    INSERT INTO dbo.DeedVerificationReason (DeedVerificationID, Reason)
    VALUES (@DeedVerificationID, @Reason);

    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_DeedVerification_GetHistory   ->  read-only, for Phase 5C
  Every verification run for a property, newest first, plus its field
  evidence and its reasons as two further result sets - mirrors
  usp_Property_GetById's multi-result-set shape (listing/images/fraud
  report) rather than usp_Fraud_GetHistory's single flattened result set,
  because this history's children are genuinely variable-cardinality rows,
  not fixed columns on the parent the way FraudCheck's 7 rule columns are.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_DeedVerification_GetHistory
    @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    -- Result set 1: parent verification runs, newest first.
    SELECT
        DeedVerificationID, PropertyID, SubmittedByUserID, GovernmentRecordID, GovernmentRecordStatus,
        VerificationStatus, Summary, SellerDocumentReference, VerifiedDate
    FROM dbo.DeedVerification
    WHERE PropertyID = @PropertyID
    ORDER BY VerifiedDate DESC, DeedVerificationID DESC;

    -- Result set 2: field evidence for every run above.
    SELECT
        f.DeedVerificationFieldID, f.DeedVerificationID, f.FieldName, f.GovernmentValue, f.SellerValue, f.IsMatch, f.Message
    FROM dbo.DeedVerificationField AS f
    INNER JOIN dbo.DeedVerification AS v ON v.DeedVerificationID = f.DeedVerificationID
    WHERE v.PropertyID = @PropertyID
    ORDER BY f.DeedVerificationID DESC, f.DeedVerificationFieldID ASC;

    -- Result set 3: reasons for every run above.
    SELECT
        r.DeedVerificationReasonID, r.DeedVerificationID, r.Reason
    FROM dbo.DeedVerificationReason AS r
    INNER JOIN dbo.DeedVerification AS v ON v.DeedVerificationID = r.DeedVerificationID
    WHERE v.PropertyID = @PropertyID
    ORDER BY r.DeedVerificationID DESC, r.DeedVerificationReasonID ASC;

    RETURN 0;
END;
GO

PRINT '>> usp_DeedVerification_Create created (Module 5B).';
PRINT '>> usp_DeedVerificationField_Add created (Module 5B).';
PRINT '>> usp_DeedVerificationReason_Add created (Module 5B).';
PRINT '>> usp_DeedVerification_GetHistory created (Module 5B).';
GO
