/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : Module6_PropertyFormVerification.sql
  Purpose : Mandatory Deed PDF + Form-vs-Deed Verification requirement.
            Extends three CHECK constraints Module5B_DeedVerification.sql
            already created (CK_DeedVerification_Status,
            CK_DeedVerificationField_FieldName, CK_DeedVerificationReason_Reason)
            so the SAME dbo.DeedVerification/DeedVerificationField/
            DeedVerificationReason tables can also record a "does the
            seller's own listing form match their own uploaded deed" run -
            no new tables. Adds one new stored procedure,
            usp_Property_ApplyDeedVerificationOutcome, which is the first
            piece of code anywhere in this schema that writes
            Property.Status = 'Approved' or '...Disapproved' AUTOMATICALLY
            (every existing write to Property.Status is either a manual
            Admin action - usp_Admin_ApproveProperty/usp_Admin_RejectProperty
            - or a lifecycle change - usp_Property_Update resetting to
            Pending on edit, usp_Property_Withdraw). Also re-issues
            usp_Admin_ApproveProperty (CREATE OR ALTER, idempotent) with one
            added guard: a property with zero DeedVerification rows can
            never be manually approved either - "a property with no deed
            must never become Approved."

  Context: this checkout does not contain the canonical Database/Scripts
  folder (see Module3_ChangePassword.sql/Module5A_FraudHistory.sql/
  Module5B_DeedVerification.sql's own header notes for the same situation).
  Please fold this into the canonical package the next time that repository
  is updated.

  Nature of the change : ADDITIVE ONLY.
    - No new table. Three existing CHECK constraints extended (guarded,
      idempotent - see each block below).
    - One new CREATE OR ALTER PROCEDURE (usp_Property_ApplyDeedVerificationOutcome).
    - usp_Admin_ApproveProperty is the only EXISTING procedure re-issued
      here (CREATE OR ALTER, the same idempotent-re-run convention every
      procedure in this schema already uses) - its logic is unchanged
      except for the one new guard described above; nothing else about it
      is touched.
  Author  : LandGuard Mandatory Deed / Form-vs-Deed Verification requirement
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  1) CK_DeedVerification_Status - add 'FormMismatch'
     (LandGuard.Domain.Enums.DeedVerificationStatus's 6th member - see that
     enum's own doc comment). Idempotent: a no-op if this script has already
     run against this database.
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerification_Status'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%FormMismatch%'
)
BEGIN
    ALTER TABLE dbo.DeedVerification DROP CONSTRAINT CK_DeedVerification_Status;
    ALTER TABLE dbo.DeedVerification ADD CONSTRAINT CK_DeedVerification_Status
        CHECK (VerificationStatus IN
            (N'Verified', N'Fraudulent', N'PriceAnomaly', N'Unverified', N'UnverifiedCancelled', N'FormMismatch'));

    PRINT '>> CK_DeedVerification_Status upgraded to include FormMismatch (Module 6).';
END
GO

/*------------------------------------------------------------------------------
  2) CK_DeedVerificationField_FieldName - add the 6 "Form"-prefixed field
     names FormDeedComparer.Compare produces (LandGuard.Application.Services.
     FormDeedComparer - inspected directly before writing this script, not
     guessed). Reusing dbo.DeedVerificationField for these rows (rather than
     a new table) - see this script's own header note.
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerificationField_FieldName'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%FormSellerNIC%'
)
BEGIN
    ALTER TABLE dbo.DeedVerificationField DROP CONSTRAINT CK_DeedVerificationField_FieldName;
    ALTER TABLE dbo.DeedVerificationField ADD CONSTRAINT CK_DeedVerificationField_FieldName
        CHECK (FieldName IN
            (N'NIC', N'OwnerName', N'DeedNumber', N'PropertyReference', N'LandSize',
             N'District', N'Address', N'Price', N'RegistrationDate',
             N'FormSellerNIC', N'FormOwnerName', N'FormDeedNumber', N'FormLocation',
             N'FormDistrict', N'FormLandSize'));

    PRINT '>> CK_DeedVerificationField_FieldName upgraded to include the 6 Form* fields (Module 6).';
END
GO

/*------------------------------------------------------------------------------
  3) CK_DeedVerificationReason_Reason - add the 6 new DeedFraudReason members
     (LandGuard.Domain.Enums.DeedFraudReason - inspected directly before
     writing this script, not guessed).
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerificationReason_Reason'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%FormSellerNicMismatch%'
)
BEGIN
    ALTER TABLE dbo.DeedVerificationReason DROP CONSTRAINT CK_DeedVerificationReason_Reason;
    ALTER TABLE dbo.DeedVerificationReason ADD CONSTRAINT CK_DeedVerificationReason_Reason
        CHECK (Reason IN
            (N'NicMismatch', N'OwnerNameMismatch', N'DeedNumberMismatch', N'PropertyReferenceMismatch',
             N'LandSizeMismatch', N'DistrictMismatch', N'AddressMismatch', N'RegistrationDateMismatch',
             N'MultipleFieldMismatch', N'PriceAnomalyDetected', N'GovernmentRecordNotFound',
             N'GovernmentRecordCancelled', N'GovernmentDocumentUnavailable',
             N'FormSellerNicMismatch', N'FormOwnerNameMismatch', N'FormDeedNumberMismatch',
             N'FormLocationMismatch', N'FormDistrictMismatch', N'FormLandSizeMismatch'));

    PRINT '>> CK_DeedVerificationReason_Reason upgraded to include the 6 Form* reasons (Module 6).';
END
GO

/*------------------------------------------------------------------------------
  3b) CK_DeedVerificationField_FieldName / CK_DeedVerificationReason_Reason -
      add 'FormOwnerNIC'/'FormOwnerAddress' and 'FormOwnerNicMismatch'/
      'FormOwnerAddressMismatch' (Owner Name / Owner NIC / Owner Address
      requirement). FormDeedComparer now produces these instead of
      'FormSellerNIC'/'FormLocation' (and no longer produces 'FormDistrict'/
      'FormLandSize' at all - see FormDeedComparer's own doc comment), but
      every OLD value stays in both CHECK lists below so a
      DeedVerificationField/DeedVerificationReason row persisted before this
      requirement still reads back without violating the constraint - purely
      additive, nothing removed.
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerificationField_FieldName'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%FormOwnerNIC%'
)
BEGIN
    ALTER TABLE dbo.DeedVerificationField DROP CONSTRAINT CK_DeedVerificationField_FieldName;
    ALTER TABLE dbo.DeedVerificationField ADD CONSTRAINT CK_DeedVerificationField_FieldName
        CHECK (FieldName IN
            (N'NIC', N'OwnerName', N'DeedNumber', N'PropertyReference', N'LandSize',
             N'District', N'Address', N'Price', N'RegistrationDate',
             N'FormSellerNIC', N'FormOwnerName', N'FormDeedNumber', N'FormLocation',
             N'FormDistrict', N'FormLandSize', N'FormOwnerNIC', N'FormOwnerAddress'));

    PRINT '>> CK_DeedVerificationField_FieldName upgraded to include FormOwnerNIC/FormOwnerAddress (Module 6).';
END
GO

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerificationReason_Reason'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%FormOwnerNicMismatch%'
)
BEGIN
    ALTER TABLE dbo.DeedVerificationReason DROP CONSTRAINT CK_DeedVerificationReason_Reason;
    ALTER TABLE dbo.DeedVerificationReason ADD CONSTRAINT CK_DeedVerificationReason_Reason
        CHECK (Reason IN
            (N'NicMismatch', N'OwnerNameMismatch', N'DeedNumberMismatch', N'PropertyReferenceMismatch',
             N'LandSizeMismatch', N'DistrictMismatch', N'AddressMismatch', N'RegistrationDateMismatch',
             N'MultipleFieldMismatch', N'PriceAnomalyDetected', N'GovernmentRecordNotFound',
             N'GovernmentRecordCancelled', N'GovernmentDocumentUnavailable',
             N'FormSellerNicMismatch', N'FormOwnerNameMismatch', N'FormDeedNumberMismatch',
             N'FormLocationMismatch', N'FormDistrictMismatch', N'FormLandSizeMismatch',
             N'FormOwnerNicMismatch', N'FormOwnerAddressMismatch'));

    PRINT '>> CK_DeedVerificationReason_Reason upgraded to include FormOwnerNicMismatch/FormOwnerAddressMismatch (Module 6).';
END
GO

/*------------------------------------------------------------------------------
  4) CK_Property_Status - add 'Disapproved' (LandGuard.Domain.Enums.
     PropertyStatus's 7th member). ROOT-CAUSE FIX: Database/Scripts/
     01_Schema.sql already carries this exact idempotent upgrade (added
     alongside the 'Withdrawn' upgrade from Phase F), but that script is the
     "from scratch" canonical script - this checkout's actual upgrade path
     for an ALREADY-EXISTING LandGuardDB is running the Module*.sql files
     standalone (see this script's own header note), and this file never
     touched CK_Property_Status at all. That is the entire reason a
     database that has only ever had Module6 (not 01_Schema.sql) rerun
     against it still reports CK_Property_Status -> AllowsDisapproved = NO
     even after usp_Property_ApplyDeedVerificationOutcome/
     usp_Property_MarkPendingForReverification/the Admin-approval guard
     above were already created by an earlier Module6 run. Same guarded
     DROP/ADD pattern as every other upgrade in this file: a no-op if this
     script (or 01_Schema.sql) has already added 'Disapproved', an actual
     upgrade otherwise, safe to re-run any number of times. Does not touch
     any existing Property row - only the constraint definition.
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

    PRINT '>> CK_Property_Status upgraded to include Disapproved (Module 6).';
END
GO

/*------------------------------------------------------------------------------
  5) OwnerName / OwnerNIC / OwnerAddress columns (Owner Name / Owner NIC /
     Owner Address requirement). Same idempotent COL_LENGTH(...) IS NULL
     guard as every other additive column in this schema, mirrored here for
     the same root-cause reason as the CK_Property_Status block above: this
     file, not Database/Scripts/01_Schema.sql, is what actually gets rerun
     against an existing LandGuardDB. No default/backfill - existing
     Property rows get NULL here (accurate - they never captured this
     data); FluentValidation + the RAISERROR guard inside the re-issued
     usp_Property_Create below are what actually make these fields
     mandatory for every NEW listing going forward.
------------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.Property', 'OwnerName') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD OwnerName NVARCHAR(150) NULL;
    PRINT '>> dbo.Property.OwnerName added (Module 6).';
END
GO

IF COL_LENGTH('dbo.Property', 'OwnerNIC') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD OwnerNIC VARCHAR(20) NULL;
    PRINT '>> dbo.Property.OwnerNIC added (Module 6).';
END
GO

IF COL_LENGTH('dbo.Property', 'OwnerAddress') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD OwnerAddress NVARCHAR(255) NULL;
    PRINT '>> dbo.Property.OwnerAddress added (Module 6).';
END
GO

/*------------------------------------------------------------------------------
  vw_PropertyListing / vw_PublishedProperty - re-issued (CREATE OR ALTER,
  identical to Database/Scripts/03_Views.sql, just with OwnerName/OwnerNIC/
  OwnerAddress added to vw_PropertyListing's column list) so the columns
  added above actually reach both views on THIS database. Re-declaring
  vw_PublishedProperty is required even though its own text is completely
  unchanged: SQL Server resolves a view's "SELECT v.*" into a fixed column
  list at CREATE VIEW time (the same reason sp_refreshview exists), so
  without this it would keep silently returning the OLD vw_PropertyListing
  column list even after vw_PropertyListing itself gains the 3 new columns.
  The stored procedures below (usp_Property_GetById/GetBySeller/Search) do
  NOT have this problem - a "SELECT * FROM view" inside a procedure body is
  resolved fresh on every execution, not cached like a view's own "SELECT *"
  is - so none of them need to be re-issued here.
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_PropertyListing
AS
SELECT
    p.PropertyID,
    p.Title,
    p.Description,
    p.Location,
    p.District,
    p.Latitude,
    p.Longitude,
    p.Size,
    p.Price,
    p.PricePerPerch,
    p.DeedReference,
    p.OwnerName,
    p.OwnerNIC,
    p.OwnerAddress,
    p.Status,
    p.UploadDate,
    p.SellerID,
    u.Name          AS SellerName,
    u.Phone         AS SellerPhone,
    u.NICVerified   AS SellerNICVerified,
    r.RiskScore,
    ISNULL(r.RiskLevel, 'Low')       AS RiskLevel,
    ISNULL(r.FraudStatus, 'Clean')   AS FraudStatus,
    r.Summary                        AS RiskSummary,
    r.GeneratedDate                  AS RiskGeneratedDate,
    (SELECT TOP (1) pi.ImageURL
       FROM dbo.PropertyImage AS pi
      WHERE pi.PropertyID = p.PropertyID
      ORDER BY pi.IsPrimary DESC, pi.ImageID ASC)          AS CoverImageURL,
    (SELECT COUNT(*) FROM dbo.PropertyImage AS pi2
      WHERE pi2.PropertyID = p.PropertyID)                 AS ImageCount,
    (SELECT COUNT(*) FROM dbo.SuspiciousReport AS sr
      WHERE sr.PropertyID = p.PropertyID)                  AS ReportCount
FROM dbo.Property AS p
INNER JOIN dbo.Users AS u
        ON u.UserID = p.SellerID
LEFT  JOIN dbo.vw_PropertyLatestRisk AS r
        ON r.PropertyID = p.PropertyID;
GO

CREATE OR ALTER VIEW dbo.vw_PublishedProperty
AS
SELECT v.*
FROM dbo.vw_PropertyListing AS v
INNER JOIN dbo.Users AS u ON u.UserID = v.SellerID
WHERE v.Status = 'Approved'
  AND u.IsActive = 1;
GO

PRINT '>> vw_PropertyListing / vw_PublishedProperty upgraded with OwnerName/OwnerNIC/OwnerAddress (Module 6).';
GO

/*------------------------------------------------------------------------------
  usp_Property_Create / usp_Property_Update - re-issued (CREATE OR ALTER,
  identical to Database/Scripts/04_StoredProcedures.sql) for the same
  root-cause reason as everything above in this section: this file is what
  actually reaches an existing LandGuardDB. usp_Property_Create's 3 new
  parameters have no default, and a RAISERROR guard rejects a missing/blank
  value for any of the 4 mandatory fields (Owner Name, Owner NIC, Owner
  Address, Deed Number) - CreatePropertyRequestValidator already blocks
  this with a clean 400 first in normal operation, so this is a
  defence-in-depth backstop, the same role the existing "Seller not found"
  check plays. usp_Property_Update keeps the same optional/ISNULL-coalesce
  pattern every other editable field already uses - omitting a field on an
  edit leaves it unchanged, never blanks it.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_Create
    @SellerID       INT,
    @Title          NVARCHAR(200),
    @Description    NVARCHAR(MAX)   = NULL,
    @Location       NVARCHAR(255),
    @District       NVARCHAR(100)   = NULL,
    @Latitude       DECIMAL(9,6)    = NULL,
    @Longitude      DECIMAL(9,6)    = NULL,
    @Size           FLOAT,
    @Price          DECIMAL(14,2),
    @DeedReference  VARCHAR(100),
    @OwnerName      NVARCHAR(150),
    @OwnerNIC       VARCHAR(20),
    @OwnerAddress   NVARCHAR(255),
    @NewPropertyID  INT             = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users
                    WHERE UserID = @SellerID AND Role = 'Seller' AND IsActive = 1)
    BEGIN
        RAISERROR (N'Seller not found, or the seller account is suspended.', 16, 1);
        RETURN -1;
    END

    IF LTRIM(RTRIM(ISNULL(@OwnerName, N'')))     = N''
    OR LTRIM(RTRIM(ISNULL(@OwnerNIC, N'')))      = N''
    OR LTRIM(RTRIM(ISNULL(@OwnerAddress, N''))) = N''
    OR LTRIM(RTRIM(ISNULL(@DeedReference, N''))) = N''
    BEGIN
        RAISERROR (N'Owner Name, Owner NIC, Owner Address and Deed Number are all required to list a property.', 16, 1);
        RETURN -2;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.Property
            (SellerID, Title, Description, Location, District, Latitude, Longitude,
             Size, Price, DeedReference, OwnerName, OwnerNIC, OwnerAddress, Status)
        VALUES
            (@SellerID, @Title, @Description, @Location, @District, @Latitude, @Longitude,
             @Size, @Price, @DeedReference, @OwnerName, @OwnerNIC, @OwnerAddress, 'Pending');

        SET @NewPropertyID = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @NewPropertyID;

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @NewPropertyID;
    RETURN 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Property_Update
    @PropertyID     INT,
    @SellerID       INT,
    @Title          NVARCHAR(200)  = NULL,
    @Description    NVARCHAR(MAX)  = NULL,
    @Location       NVARCHAR(255)  = NULL,
    @District       NVARCHAR(100)  = NULL,
    @Latitude       DECIMAL(9,6)   = NULL,
    @Longitude      DECIMAL(9,6)   = NULL,
    @Size           FLOAT          = NULL,
    @Price          DECIMAL(14,2)  = NULL,
    @DeedReference  VARCHAR(100)   = NULL,
    @OwnerName      NVARCHAR(150)  = NULL,
    @OwnerNIC       VARCHAR(20)    = NULL,
    @OwnerAddress   NVARCHAR(255)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property
                    WHERE PropertyID = @PropertyID AND SellerID = @SellerID)
    BEGIN
        RAISERROR (N'Property not found, or it does not belong to this seller.', 16, 1);
        RETURN -1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID AND Status = 'Withdrawn')
    BEGIN
        RAISERROR (N'This listing has been withdrawn and cannot be edited. Relisting is not currently supported.', 16, 1);
        RETURN -2;
    END

    UPDATE dbo.Property
    SET Title         = ISNULL(@Title,         Title),
        Description   = ISNULL(@Description,   Description),
        Location      = ISNULL(@Location,      Location),
        District      = ISNULL(@District,      District),
        Latitude      = ISNULL(@Latitude,      Latitude),
        Longitude     = ISNULL(@Longitude,     Longitude),
        Size          = ISNULL(@Size,          Size),
        Price         = ISNULL(@Price,         Price),
        DeedReference = ISNULL(@DeedReference, DeedReference),
        OwnerName     = ISNULL(@OwnerName,     OwnerName),
        OwnerNIC      = ISNULL(@OwnerNIC,      OwnerNIC),
        OwnerAddress  = ISNULL(@OwnerAddress,  OwnerAddress),
        Status        = CASE WHEN Status = 'Disapproved' THEN Status ELSE 'Pending' END
    WHERE PropertyID = @PropertyID;

    EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @PropertyID;

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @PropertyID;
    RETURN 0;
END;
GO

PRINT '>> usp_Property_Create / usp_Property_Update upgraded with OwnerName/OwnerNIC/OwnerAddress (Module 6).';
GO

/*------------------------------------------------------------------------------
  usp_Property_ApplyDeedVerificationOutcome
  ------------------------------------------------------------------------------
  Called once by GovernmentDeedVerificationService.VerifyAndPersistAsync,
  immediately after PersistAsync (the DeedVerification/Field/Reason insert)
  succeeds - ONLY for a @VerificationStatus of 'Verified', 'FormMismatch',
  'Fraudulent' or 'PriceAnomaly'. The caller deliberately does NOT invoke
  this procedure for 'Unverified'/'UnverifiedCancelled' (an OCR/technical
  failure, or no government record/document available) - per the Mandatory
  Deed / Form-vs-Deed Verification requirement's own "do not automatically
  label the listing fraudulent solely because of a technical failure" rule,
  those two outcomes leave Property.Status exactly where it already was.

  This is a SYSTEM-AUTOMATED transition, not an Admin action: no
  dbo.AdminAction row is inserted (that table's AdminID column is a NOT NULL
  FK to an actual admin - there is no automated-system id to attribute this
  to, and attributing it to a fake/borrowed admin id would misrepresent who
  made the decision). usp_Admin_ApproveProperty/usp_Admin_RejectProperty
  remain the only procedures that insert AdminAction rows.

  Mapping (per the Mandatory Deed / Form-vs-Deed Verification requirement's
  own Step 3/4):
    'Verified'      -> Status = 'Approved'    (every registry field matched)
    'FormMismatch'       -> Status = 'Disapproved' (seller's own form disagrees with their own deed - never reaches Government Registry review)
    'Fraudulent'         -> Status = 'Disapproved' (a material Government Registry field mismatch - NIC/owner/deed number/reference/land size/district/address/registration date)
    'Unverified'         -> Status = 'Disapproved' (CORRECTED: government record genuinely not found, or found-but-its-document-unreadable/"invalid deed" - both are IGovernmentRegistryService successfully answering "no", never a technical failure - see DeedVerificationStatus.Unverified's own doc comment)
    'UnverifiedCancelled' -> Status = 'Disapproved' (CORRECTED: government record found but Cancelled/Suspended - also a successful, authoritative negative answer, never a technical failure)
    'PriceAnomaly'       -> Status = 'Pending'     (stays/returns to the existing Admin review queue - vw_FlaggedProperty already matches 'Pending') - ONLY reached when price is the sole detected problem; GovernmentDeedFraudDetectionService.ClassifyMismatch already checks every material field before price, so PriceAnomaly can never win over a simultaneous material mismatch

  A genuine TECHNICAL failure (registry service unavailable, network error,
  timeout, unexpected API failure, or the seller's own deed OCR failing) is
  never represented by any DeedVerificationStatus value at all - it is a
  thrown exception or a Result.Failure returned before
  GovernmentDeedFraudDetectionService.Classify ever runs, so this procedure
  is never even called for that case and Property.Status is left exactly
  where it was. See GovernmentDeedVerificationService.VerifyAndPersistAsync's
  own inline comment for the full chain of reasoning.

  A Withdrawn property is left untouched (RAISERROR, same guard
  usp_Admin_ApproveProperty/usp_Property_Update already apply) - a Seller's
  own withdrawal decision must not be silently overridden by a verification
  result. Every other current status (Pending, Approved, Flagged, Rejected,
  Disapproved) is a valid source state: a fresh verification's outcome is
  authoritative regardless of what the property's previous status was,
  including moving a previously-Approved property back out of Approved if a
  RE-verification (SellerDeedVerificationSection's "Replace / Re-verify
  Deed" - independent of usp_Property_Update, so it can run without any
  form-field edit at all) now disagrees - "must not remain silently
  Approved" per the requirement's own Step 7.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_ApplyDeedVerificationOutcome
    @PropertyID         INT,
    @VerificationStatus VARCHAR(30),
    @Summary             NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentStatus VARCHAR(20), @Title NVARCHAR(200), @SellerID INT;
    SELECT @CurrentStatus = Status, @Title = Title, @SellerID = SellerID
    FROM dbo.Property WHERE PropertyID = @PropertyID;

    IF @SellerID IS NULL
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    IF @CurrentStatus = 'Withdrawn'
    BEGIN
        RAISERROR (N'This listing has been withdrawn by the seller and cannot be re-verified into a new status.', 16, 1);
        RETURN -2;
    END

    DECLARE @NewStatus VARCHAR(20), @Message NVARCHAR(600);

    IF @VerificationStatus = 'Verified'
    BEGIN
        SET @NewStatus = 'Approved';
        SET @Message = N'Your listing "' + @Title + N'" has passed automated deed and Government Registry verification and is now live to buyers.';
    END
    ELSE IF @VerificationStatus = 'FormMismatch'
    BEGIN
        SET @NewStatus = 'Disapproved';
        SET @Message = N'Your listing "' + @Title + N'" has been disapproved. ' + ISNULL(@Summary, N'The property information you entered does not match your uploaded deed.');
    END
    ELSE IF @VerificationStatus = 'Fraudulent'
    BEGIN
        SET @NewStatus = 'Disapproved';
        SET @Message = N'Your listing "' + @Title + N'" has been disapproved. ' + ISNULL(@Summary, N'The uploaded deed does not match the Government Registry record.');
    END
    ELSE IF @VerificationStatus = 'Unverified'
    BEGIN
        SET @NewStatus = 'Disapproved';
        SET @Message = N'Your listing "' + @Title + N'" has been disapproved. ' + ISNULL(@Summary, N'No matching Government Registry record could be found, or the government deed document could not be validated.');
    END
    ELSE IF @VerificationStatus = 'UnverifiedCancelled'
    BEGIN
        SET @NewStatus = 'Disapproved';
        SET @Message = N'Your listing "' + @Title + N'" has been disapproved. ' + ISNULL(@Summary, N'The Government Registry record for this property is cancelled.');
    END
    ELSE IF @VerificationStatus = 'PriceAnomaly'
    BEGIN
        SET @NewStatus = 'Pending';
        SET @Message = N'Your listing "' + @Title + N'" requires manual review before it can be approved. ' + ISNULL(@Summary, N'A price anomaly was detected during deed verification.');
    END
    ELSE
    BEGIN
        RAISERROR (N'usp_Property_ApplyDeedVerificationOutcome does not handle VerificationStatus ''%s''.', 16, 1, @VerificationStatus);
        RETURN -3;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Property SET Status = @NewStatus WHERE PropertyID = @PropertyID;

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID, @Message, @PropertyID);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @PropertyID;
    RETURN 0;
END;
GO

PRINT '>> usp_Property_ApplyDeedVerificationOutcome created (Module 6).';
GO

/*------------------------------------------------------------------------------
  usp_Property_MarkPendingForReverification
  ------------------------------------------------------------------------------
  Status-safety correction. Called by
  GovernmentDeedComparisonService.CompareAsync the instant a re-verification
  begins on an owned property - immediately after that method's own
  ownership check, before any OCR or Government Registry I/O that could
  fail. Without this, an already-Approved property that undergoes a fresh
  "Replace / Re-verify Deed" run would stay Approved (and therefore
  Buyer-visible - vw_PublishedProperty only ever returns Status =
  'Approved') for the entire duration of that attempt; if the attempt then
  fails technically (OCR failure, network error, timeout, unexpected
  Government Registry API failure), nothing downstream ever calls
  usp_Property_ApplyDeedVerificationOutcome to correct it, so the stale
  Approved status would otherwise persist indefinitely.

  Deliberately a NO-OP for every status except 'Approved':
    'Pending'     -> already not Buyer-visible; remains Pending while this run processes.
    'Disapproved' -> a SYSTEM-AUTOMATED verdict already in place; stays Disapproved while this run processes - only a fresh usp_Property_ApplyDeedVerificationOutcome call (i.e. this same re-verification actually completing) is allowed to move it out, never merely starting one.
    'Flagged'/'Rejected' -> unrelated to this workflow; left untouched.
    'Withdrawn'   -> a seller's own withdrawal decision must never be silently reopened by starting a re-verification - left untouched, no error (mirrors this script's other "leave Withdrawn alone" guards, but as a silent no-op here rather than a RAISERROR, since starting a verification attempt itself is not the destructive action Withdrawn otherwise guards against).

  Once this call has (conditionally) run, the rest of the verification
  pipeline behaves exactly as usp_Property_ApplyDeedVerificationOutcome's
  own header comment already documents: Verified -> Approved (moves back
  out of the Pending this procedure may have just set), FormMismatch/
  Fraudulent/Unverified/UnverifiedCancelled -> Disapproved, PriceAnomaly ->
  Pending (already Pending from here - idempotent), and a technical
  failure that never reaches classification at all leaves the property
  exactly where this procedure left it: Pending, hidden from Buyers,
  retryable.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_MarkPendingForReverification
    @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentStatus VARCHAR(20), @Title NVARCHAR(200), @SellerID INT;
    SELECT @CurrentStatus = Status, @Title = Title, @SellerID = SellerID
    FROM dbo.Property WHERE PropertyID = @PropertyID;

    IF @SellerID IS NULL
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    IF @CurrentStatus <> 'Approved'
    BEGIN
        RETURN 0;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Property SET Status = 'Pending' WHERE PropertyID = @PropertyID;

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID,
                N'Your listing "' + @Title + N'" has been temporarily unpublished while a new deed verification is processed. It will not be visible to buyers until verification completes.',
                @PropertyID);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    RETURN 0;
END;
GO

PRINT '>> usp_Property_MarkPendingForReverification created (Module 6).';
GO

/*------------------------------------------------------------------------------
  usp_Admin_ApproveProperty - re-issued (CREATE OR ALTER, unchanged
  otherwise) with one added guard: "a property with no deed must never
  become Approved" applies to a manual Admin approval exactly as much as it
  applies to the new automated path above. Gate is "at least one
  DeedVerification row exists for this property" (regardless of that row's
  own outcome) - deliberately NOT "the latest run was Verified", since an
  Admin reviewing a PriceAnomaly-flagged listing (the existing, intended
  purpose of manual approval) must still be able to approve it after their
  own judgement call; this guard only ever blocks the one case the
  requirement actually describes - a deed that was never uploaded/verified
  at all.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_ApproveProperty
    @AdminID    INT,
    @PropertyID INT,
    @Remarks    NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @AdminID AND Role = 'Admin' AND IsActive = 1)
    BEGIN
        RAISERROR (N'Only an active administrator can approve listings.', 16, 1);
        RETURN -1;
    END

    DECLARE @SellerID INT, @Title NVARCHAR(200), @CurrentStatus VARCHAR(20);
    SELECT @SellerID = SellerID, @Title = Title, @CurrentStatus = Status
    FROM dbo.Property WHERE PropertyID = @PropertyID;

    IF @SellerID IS NULL
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -2;
    END

    -- Phase F (Property Withdrawal): a Withdrawn listing has left the
    -- active moderation workflow by the seller's own choice - normal
    -- Approve/Reject moderation must not resurrect it. Pending properties
    -- are unaffected by this check.
    IF @CurrentStatus = 'Withdrawn'
    BEGIN
        RAISERROR (N'This listing has been withdrawn by the seller and is no longer part of the active review queue.', 16, 1);
        RETURN -3;
    END

    -- Mandatory Deed / Form-vs-Deed Verification requirement: "a property
    -- with no deed must never become Approved." See this script's own
    -- header note for exactly why this checks for existence, not outcome.
    IF NOT EXISTS (SELECT 1 FROM dbo.DeedVerification WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'This listing cannot be approved because no deed document has been uploaded and verified for it yet.', 16, 1);
        RETURN -4;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Property SET Status = 'Approved' WHERE PropertyID = @PropertyID;

        INSERT INTO dbo.AdminAction (AdminID, ActionType, PropertyID, Remarks)
        VALUES (@AdminID, 'ApproveListing', @PropertyID, @Remarks);

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID,
                N'Your listing "' + @Title + N'" has been reviewed and approved by an administrator.',
                @PropertyID);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @PropertyID;
    RETURN 0;
END;
GO

PRINT '>> usp_Admin_ApproveProperty upgraded with a deed-verification-existence guard (Module 6).';
GO
