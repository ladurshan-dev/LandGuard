/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : 04_StoredProcedures.sql
  Purpose : Functions and stored procedures backing every API endpoint listed in
            the API Development plan (Phases 1-4).
  Author  : Ladhurshan Sivasathyamoorthy
  ------------------------------------------------------------------------------
  CONTENTS
    A. Scalar functions
    B. Authentication      -> AuthController
    C. Property CRUD       -> PropertyController
    D. Fraud engine        -> FraudDetectionService + RiskReportService
    E. Buyer features      -> saved properties, suspicious reports
    F. Admin features      -> AdminController
    G. Notifications & podcasts
==============================================================================*/

USE LandGuardDB;
GO

/*==============================================================================
  A. SCALAR FUNCTIONS
==============================================================================*/

/*------------------------------------------------------------------------------
  fn_IsValidNIC - Sri Lankan NIC format validation.
  Old format: 9 digits + V or X.   New format: 12 digits.
  NOTE (Chapter 2.1 limitation): format validation only. No government API is
  available, so this simulates identity verification.
------------------------------------------------------------------------------*/
CREATE OR ALTER FUNCTION dbo.fn_IsValidNIC (@NIC VARCHAR(20))
RETURNS BIT
AS
BEGIN
    DECLARE @Result BIT = 0;
    SET @NIC = LTRIM(RTRIM(ISNULL(@NIC, '')));

    IF @NIC LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][VvXx]'
        SET @Result = 1;
    ELSE IF @NIC LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
        SET @Result = 1;

    RETURN @Result;
END;
GO

/*------------------------------------------------------------------------------
  fn_RiskLevelFromScore - FR05 banding: Low 0-40, Medium 41-70, High 71-100
------------------------------------------------------------------------------*/
CREATE OR ALTER FUNCTION dbo.fn_RiskLevelFromScore (@Score INT)
RETURNS VARCHAR(20)
AS
BEGIN
    RETURN CASE
             WHEN @Score <= 40 THEN 'Low'
             WHEN @Score <= 70 THEN 'Medium'
             ELSE 'High'
           END;
END;
GO

/*------------------------------------------------------------------------------
  fn_GetRuleWeight - reads a rule weight, returning 0 when the rule is disabled
------------------------------------------------------------------------------*/
CREATE OR ALTER FUNCTION dbo.fn_GetRuleWeight (@RuleCode VARCHAR(30))
RETURNS INT
AS
BEGIN
    RETURN ISNULL((SELECT CASE WHEN IsEnabled = 1 THEN Weight ELSE 0 END
                   FROM dbo.FraudRuleWeight WHERE RuleCode = @RuleCode), 0);
END;
GO


/*==============================================================================
  B. AUTHENTICATION   ->  AuthController
==============================================================================*/

/*------------------------------------------------------------------------------
  usp_User_Register   ->  POST /api/auth/register
  The password is hashed in the API layer (BCrypt); only the hash reaches SQL.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_User_Register
    @Name           NVARCHAR(150),
    @Email          NVARCHAR(150),
    @PasswordHash   NVARCHAR(255),
    @Role           VARCHAR(20),
    @NIC            VARCHAR(20)   = NULL,
    @Phone          VARCHAR(20)   = NULL,
    @NewUserID      INT           = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Role NOT IN ('Buyer','Seller','Admin')
    BEGIN
        RAISERROR (N'Invalid role. Allowed values: Buyer, Seller, Admin.', 16, 1);
        RETURN -1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
    BEGIN
        RAISERROR (N'This email address is already registered.', 16, 1);
        RETURN -2;
    END

    -- FR02: a seller must provide a correctly formatted NIC
    IF @Role = 'Seller'
    BEGIN
        IF @NIC IS NULL OR dbo.fn_IsValidNIC(@NIC) = 0
        BEGIN
            RAISERROR (N'A valid Sri Lankan NIC is required to register as a seller.', 16, 1);
            RETURN -3;
        END
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE NIC = @NIC)
        BEGIN
            RAISERROR (N'This NIC is already linked to another account.', 16, 1);
            RETURN -4;
        END
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.Users (Name, Email, PasswordHash, NIC, Phone, Role, NICVerified)
        VALUES (@Name, @Email, @PasswordHash, @NIC, @Phone, @Role,
                CASE WHEN @Role = 'Seller' AND dbo.fn_IsValidNIC(@NIC) = 1 THEN 1 ELSE 0 END);

        SET @NewUserID = SCOPE_IDENTITY();

        INSERT INTO dbo.Notification (UserID, Message)
        VALUES (@NewUserID, N'Welcome to LandGuard. Your ' + @Role + N' account has been created.');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT UserID, Name, Email, Role, NIC, Phone, NICVerified, IsActive, CreatedAt
    FROM dbo.Users WHERE UserID = @NewUserID;

    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_User_Login   ->  POST /api/auth/login
  Returns the stored hash so the API can verify it and then issue a JWT.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_User_Login
    @Email NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UserID, Name, Email, PasswordHash, Role, NIC, Phone, NICVerified, IsActive
    FROM dbo.Users
    WHERE Email = @Email;
END;
GO

/*------------------------------------------------------------------------------
  usp_User_GetById / usp_User_SetActive
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_User_GetById
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserID, Name, Email, Role, NIC, Phone, NICVerified, IsActive, IdentityStatus, CreatedAt
    FROM dbo.Users WHERE UserID = @UserID;
END;
GO

/*------------------------------------------------------------------------------
  usp_User_SetIdentityStatus   ->  Seller Government Identity Verification
  requirement. The only procedure that writes dbo.Users.IdentityStatus -
  called by Application layer's SellerIdentityVerificationService right
  after a Seller registers, and again from the Seller-authenticated
  identity/reverify endpoint. RAISERRORs for a non-Seller, the same
  defence-in-depth style usp_Property_Create's Owner-field guard uses.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_User_SetIdentityStatus
    @UserID         INT,
    @IdentityStatus VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF @IdentityStatus NOT IN ('Pending', 'Verified', 'Failed')
    BEGIN
        RAISERROR (N'Invalid identity status. Allowed values: Pending, Verified, Failed.', 16, 1);
        RETURN -1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @UserID AND Role = 'Seller')
    BEGIN
        RAISERROR (N'Identity status only applies to a Seller account.', 16, 1);
        RETURN -2;
    END

    -- Kept in lockstep with the legacy NICVerified BIT so the two can never
    -- contradict each other - NICVerified still feeds CHECK 3 of
    -- usp_Fraud_AnalyseProperty and is projected to the frontend as the
    -- Buyer/Admin-facing "(NIC verified)" badge (SellerNicVerified on
    -- PropertyListingResult/PropertySearchResult). Verified -> 1,
    -- Pending/Failed -> 0.
    UPDATE dbo.Users
    SET IdentityStatus = @IdentityStatus,
        NICVerified = CASE WHEN @IdentityStatus = 'Verified' THEN 1 ELSE 0 END
    WHERE UserID = @UserID;

    SELECT UserID, Name, Email, Role, NIC, Phone, NICVerified, IsActive, IdentityStatus, CreatedAt
    FROM dbo.Users WHERE UserID = @UserID;

    RETURN 0;
END;
GO


/*==============================================================================
  C. PROPERTY CRUD   ->  PropertyController
==============================================================================*/

/*------------------------------------------------------------------------------
  usp_Property_Create   ->  POST /api/properties
  Inserts the listing and immediately runs the 8-point fraud engine (NFR04:
  the engine must execute on every submission).
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

    -- Seller Government Identity Verification requirement: defence-in-depth
    -- backing up PropertyService.CreateAsync's own check (which runs first,
    -- with the exact Seller-facing message this requirement specifies).
    IF NOT EXISTS (SELECT 1 FROM dbo.Users
                    WHERE UserID = @SellerID AND IdentityStatus = 'Verified')
    BEGIN
        RAISERROR (N'Your identity must be verified before you can list a property.', 16, 1);
        RETURN -7;
    END

    -- Owner Name / Owner NIC / Owner Address / Deed Number requirement:
    -- all four are mandatory for every new listing. CreatePropertyRequestValidator
    -- already rejects a missing/blank value with a clean 400 before this is
    -- ever reached, so in normal operation this RAISERROR is a defence-in-
    -- depth backstop (the same role the Seller-not-found check above
    -- plays) - not the primary enforcement point.
    IF LTRIM(RTRIM(ISNULL(@OwnerName, N'')))     = N''
    OR LTRIM(RTRIM(ISNULL(@OwnerNIC, N'')))      = N''
    OR LTRIM(RTRIM(ISNULL(@OwnerAddress, N''))) = N''
    OR LTRIM(RTRIM(ISNULL(@DeedReference, N''))) = N''
    BEGIN
        RAISERROR (N'Owner Name, Owner NIC, Owner Address and Deed Number are all required to list a property.', 16, 1);
        RETURN -2;
    END

    -- Global Duplicate-Property Prevention requirement: see
    -- Module7_IdentityAndDuplicateProperty.sql's header comment for why a
    -- schema-level UNIQUE index is unsafe here (05_SeedData.sql plants 7
    -- deliberate duplicate-DeedReference pairs to exercise the legacy fraud
    -- engine's own duplicate-deed rule) and exactly how this
    -- sp_getapplock-serialized check prevents two concurrent Create calls
    -- for the same deed from both succeeding.
    DECLARE @NormalizedDeedReference NVARCHAR(100) = UPPER(LTRIM(RTRIM(@DeedReference)));

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @LockResource NVARCHAR(255) = N'Property_DeedReference_' + @NormalizedDeedReference;
        DECLARE @LockResult INT;
        EXEC @LockResult = sp_getapplock
            @Resource = @LockResource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;

        IF @LockResult < 0
        BEGIN
            RAISERROR (N'Could not verify deed uniqueness at this time. Please try again.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN -8;
        END

        IF EXISTS (SELECT 1 FROM dbo.Property WHERE UPPER(LTRIM(RTRIM(DeedReference))) = @NormalizedDeedReference)
        BEGIN
            RAISERROR (N'This property/deed is already listed in LandGuard.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN -9;
        END

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

    -- Images are attached by the API immediately afterwards; the engine is then
    -- re-run by usp_Fraud_AnalyseProperty so the image rules see the uploads.
    EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @NewPropertyID;

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @NewPropertyID;
    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_FindByGovernmentPropertyReference   ->  Global
  Duplicate-Property Prevention requirement. Read-only, privacy-safe lookup:
  PropertyID only - never SellerID or any Seller PII - so the caller
  structurally cannot leak another Seller's private details even by
  accident. Excludes the property currently being verified.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_FindByGovernmentPropertyReference
    @GovernmentPropertyReference NVARCHAR(50),
    @ExcludePropertyID           INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1) PropertyID
    FROM dbo.Property
    WHERE GovernmentPropertyReference = @GovernmentPropertyReference
      AND PropertyID <> @ExcludePropertyID;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_ApplyDeedVerificationOutcome   ->  called by
  GovernmentDeedVerificationService.VerifyAndPersistAsync (Mandatory Deed /
  Form-vs-Deed Verification requirement + Global Duplicate-Property
  Prevention requirement).

  ADDED TO CANONICAL (post-review): this procedure previously existed only
  in Module6_PropertyFormVerification.sql / Module7_IdentityAndDuplicateProperty.sql,
  never in this canonical file - a fresh install built from
  01_Schema.sql-04_StoredProcedures.sql alone (00_RunAll.sql) would
  silently be missing it, so any Seller deed-verification attempt against
  a from-scratch database would fail with an unresolvable stored-procedure
  error. Safe to add here standalone: this procedure only ever reads/writes
  dbo.Property and dbo.Notification (both already in this canonical
  script) plus dbo.vw_PropertyListing (03_Views.sql) - it does NOT touch
  dbo.DeedVerification or its two child tables at all, so it does not
  require Module5B's DeedVerication-table schema to exist. This is the
  FINAL current version - same body as Module7's re-issue, in sync with:
    Verified            -> Approved
    PriceAnomaly         -> Pending
    FormMismatch          -> Disapproved
    Fraudulent            -> Disapproved
    Unverified            -> Disapproved
    UnverifiedCancelled   -> Disapproved
    DuplicateProperty     -> Disapproved
  plus GovernmentPropertyReference persistence and the concurrency-safe
  sp_getapplock-protected duplicate-reference check (see the inline
  comments below - identical to Module7's own "CONCURRENCY FIX" and
  "AUDIT-CONSISTENCY FIX" comments, not reproduced in full here to avoid
  duplicating that explanation; read Module7_IdentityAndDuplicateProperty.sql
  for the complete reasoning).

  NOTE ON FRESH-INSTALL COMPLETENESS: adding this one procedure does not by
  itself make 00_RunAll.sql produce a fully-featured fresh database.
  dbo.DeedVerification/DeedVerificationField/DeedVerificationReason (and
  their four stored procedures - usp_DeedVerification_Create,
  usp_DeedVerificationField_Add, usp_DeedVerificationReason_Add,
  usp_DeedVerification_GetHistory, all defined only in
  Module5B_DeedVerification.sql) are NOT part of this canonical schema at
  all - GovernmentDeedVerificationStoredProcedures.PersistAsync would still
  fail against a fresh 00_RunAll.sql-only database, this procedure's own
  presence notwithstanding. usp_User_ChangePassword (Module3),
  usp_Fraud_GetHistory (Module5A) and the Document Comparison procedures
  (Module5C) are likewise still canonical-only gaps. This was investigated
  and is reported in full to the person who requested this fix; it was not
  silently expanded into fixing everything in this one pass.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_ApplyDeedVerificationOutcome
    @PropertyID                    INT,
    @VerificationStatus            VARCHAR(30),
    @Summary                        NVARCHAR(500) = NULL,
    @GovernmentPropertyReference   NVARCHAR(50)   = NULL,
    @EffectiveVerificationStatus   VARCHAR(30)    = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @EffectiveVerificationStatus = @VerificationStatus;

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
    DECLARE @PersistReference BIT = 0;

    IF @VerificationStatus = 'Verified'
    BEGIN
        SET @NewStatus = 'Approved';
        SET @Message = N'Your listing "' + @Title + N'" has passed automated deed and Government Registry verification and is now live to buyers.';
        SET @PersistReference = 1;
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
    ELSE IF @VerificationStatus = 'DuplicateProperty'
    BEGIN
        SET @NewStatus = 'Disapproved';
        SET @Message = N'Listing Disapproved — This property is already registered as another LandGuard listing.';
    END
    ELSE IF @VerificationStatus = 'PriceAnomaly'
    BEGIN
        SET @NewStatus = 'Pending';
        SET @Message = N'Your listing "' + @Title + N'" requires manual review before it can be approved. ' + ISNULL(@Summary, N'A price anomaly was detected during deed verification.');
        SET @PersistReference = 1;
    END
    ELSE
    BEGIN
        RAISERROR (N'usp_Property_ApplyDeedVerificationOutcome does not handle VerificationStatus ''%s''.', 16, 1, @VerificationStatus);
        RETURN -3;
    END

    DECLARE @NormalizedGovRef NVARCHAR(50) = NULL;
    IF @PersistReference = 1 AND LTRIM(RTRIM(ISNULL(@GovernmentPropertyReference, N''))) <> N''
        SET @NormalizedGovRef = UPPER(LTRIM(RTRIM(@GovernmentPropertyReference)));
    ELSE
        SET @PersistReference = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @PersistReference = 1
        BEGIN
            -- Concurrency-safe duplicate check under sp_getapplock - same
            -- pattern as usp_Property_Create's DeedReference guard, on a
            -- distinct lock-resource namespace. See
            -- Module7_IdentityAndDuplicateProperty.sql's matching
            -- procedure for the full "CONCURRENCY FIX" explanation.
            DECLARE @GovRefLockResource NVARCHAR(255) = N'Property_GovRef_' + @NormalizedGovRef;
            DECLARE @GovRefLockResult INT;
            EXEC @GovRefLockResult = sp_getapplock
                @Resource = @GovRefLockResource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;

            IF @GovRefLockResult < 0
            BEGIN
                RAISERROR (N'Could not verify government property-reference uniqueness at this time. Please try again.', 16, 1);
                ROLLBACK TRANSACTION;
                RETURN -4;
            END

            IF EXISTS (
                SELECT 1 FROM dbo.Property
                WHERE UPPER(LTRIM(RTRIM(GovernmentPropertyReference))) = @NormalizedGovRef
                  AND PropertyID <> @PropertyID
            )
            BEGIN
                SET @NewStatus = 'Disapproved';
                SET @Message = N'Listing Disapproved — This property is already registered as another LandGuard listing.';
                SET @PersistReference = 0;
                SET @EffectiveVerificationStatus = 'DuplicateProperty';
            END
        END

        UPDATE dbo.Property
        SET Status = @NewStatus,
            GovernmentPropertyReference =
                CASE WHEN @PersistReference = 1 THEN @GovernmentPropertyReference ELSE GovernmentPropertyReference END
        WHERE PropertyID = @PropertyID;

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

/*------------------------------------------------------------------------------
  usp_PropertyImage_Add   ->  part of POST /api/properties
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_PropertyImage_Add
    @PropertyID INT,
    @ImageURL   NVARCHAR(500),
    @ImageHash  VARCHAR(255) = NULL,
    @IsPrimary  BIT          = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    IF @IsPrimary = 1
        UPDATE dbo.PropertyImage SET IsPrimary = 0 WHERE PropertyID = @PropertyID;

    INSERT INTO dbo.PropertyImage (PropertyID, ImageURL, ImageHash, IsPrimary)
    VALUES (@PropertyID, @ImageURL, @ImageHash, @IsPrimary);

    SELECT SCOPE_IDENTITY() AS ImageID;
    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_PropertyImage_Delete   ->  DELETE /api/properties/{id}/images/{imageId}

  No ownership check here, deliberately mirroring usp_PropertyImage_Add
  (which also has none) rather than usp_Property_Delete (which enforces
  ownership itself via RAISERROR): PropertyService.DeleteImageAsync
  already resolves the property + image and enforces "owner or Admin" in
  C# before this procedure is ever called, the same split AddImageAsync
  already uses for this exact sub-resource. Deleting only this one row -
  it never touches Property, FraudCheck, RiskReport or any other
  PropertyImage row's ImageHash/ImageURL.

  Primary-image rule: if the deleted image was primary and other images
  remain for the property, the oldest remaining image (lowest ImageID)
  is promoted to primary in the same call, so a property is never left
  with zero primary images while images still exist, and never ends up
  with more than one.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_PropertyImage_Delete
    @PropertyID INT,
    @ImageID    INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.PropertyImage WHERE ImageID = @ImageID AND PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Image not found.', 16, 1);
        RETURN -1;
    END

    DECLARE @WasPrimary BIT;
    SELECT @WasPrimary = IsPrimary FROM dbo.PropertyImage WHERE ImageID = @ImageID;

    DELETE FROM dbo.PropertyImage WHERE ImageID = @ImageID;

    IF @WasPrimary = 1
    BEGIN
        DECLARE @NewPrimaryImageID INT;

        SELECT TOP 1 @NewPrimaryImageID = ImageID
        FROM dbo.PropertyImage
        WHERE PropertyID = @PropertyID
        ORDER BY ImageID ASC;

        IF @NewPrimaryImageID IS NOT NULL
            UPDATE dbo.PropertyImage SET IsPrimary = 1 WHERE ImageID = @NewPrimaryImageID;
    END

    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_GetById   ->  GET /api/properties/{id}
  Returns 3 result sets: the listing, its images, and the rule-by-rule report.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_GetById
    @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @PropertyID;

    SELECT ImageID, ImageURL, ImageHash, IsPrimary, UploadedDate
    FROM dbo.PropertyImage
    WHERE PropertyID = @PropertyID
    ORDER BY IsPrimary DESC, ImageID;

    SELECT RuleCode, RuleName, Triggered, PointsAdded, MaxPoints, Description
    FROM dbo.vw_FraudCheckDetail
    WHERE PropertyID = @PropertyID
    ORDER BY PointsAdded DESC, RuleCode;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_Search   ->  GET /api/properties     (FR10)
  Paged, filterable search over published listings.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_Search
    @Keyword     NVARCHAR(200) = NULL,
    @District    NVARCHAR(100) = NULL,
    @MinPrice    DECIMAL(14,2) = NULL,
    @MaxPrice    DECIMAL(14,2) = NULL,
    @MinSize     FLOAT         = NULL,
    @MaxSize     FLOAT         = NULL,
    @RiskLevel   VARCHAR(20)   = NULL,   -- Low / Medium / High
    @SortBy      VARCHAR(20)   = 'Newest', -- Newest | Oldest | PriceAsc | PriceDesc | RiskAsc
    @PageNumber  INT           = 1,
    @PageSize    INT           = 12
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize  < 1 SET @PageSize  = 12;
    IF @PageSize  > 100 SET @PageSize = 100;

    ;WITH Filtered AS
    (
        SELECT *
        FROM dbo.vw_PublishedProperty
        WHERE (@Keyword   IS NULL OR Title    LIKE '%' + @Keyword + '%'
                                  OR Location LIKE '%' + @Keyword + '%'
                                  OR District LIKE '%' + @Keyword + '%')
          AND (@District  IS NULL OR District  = @District)
          AND (@MinPrice  IS NULL OR Price    >= @MinPrice)
          AND (@MaxPrice  IS NULL OR Price    <= @MaxPrice)
          AND (@MinSize   IS NULL OR Size     >= @MinSize)
          AND (@MaxSize   IS NULL OR Size     <= @MaxSize)
          AND (@RiskLevel IS NULL OR RiskLevel = @RiskLevel)
    )
    SELECT *,
           (SELECT COUNT(*) FROM Filtered) AS TotalRecords
    FROM Filtered
    ORDER BY
        CASE WHEN @SortBy = 'PriceAsc'  THEN Price      END ASC,
        CASE WHEN @SortBy = 'PriceDesc' THEN Price      END DESC,
        CASE WHEN @SortBy = 'RiskAsc'   THEN RiskScore  END ASC,
        CASE WHEN @SortBy = 'Oldest'    THEN PropertyID END ASC,
        CASE WHEN @SortBy = 'Newest'    THEN PropertyID END DESC,
        PropertyID DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_GetBySeller   ->  seller dashboard listing grid (FR08)
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_GetBySeller
    @SellerID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.vw_PropertyListing
    WHERE SellerID = @SellerID
    ORDER BY UploadDate DESC;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_Update   ->  PUT /api/properties/{id}
  A seller correcting a flagged listing puts it back to Pending and re-runs the
  engine (supports the "view reasons and resubmit" user story).
------------------------------------------------------------------------------*/
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

    -- Phase F (Property Withdrawal): a Withdrawn listing is not reachable
    -- through the normal edit flow, because this procedure unconditionally
    -- resets Status back to 'Pending' below - without this guard, editing a
    -- withdrawn listing would silently "revive" it back into the active
    -- moderation workflow. There is no "Relist" action yet; that is a
    -- deliberate, separate future decision, not implemented here.
    IF EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID AND Status = 'Withdrawn')
    BEGIN
        RAISERROR (N'This listing has been withdrawn and cannot be edited. Relisting is not currently supported.', 16, 1);
        RETURN -2;
    END

    -- CORRECTED (Mandatory Deed / Form-vs-Deed Verification requirement,
    -- Seller Edit Status Protection): a Disapproved listing is a
    -- SYSTEM-AUTOMATED verdict (Form-vs-Deed mismatch, or any non-price
    -- Government Registry mismatch - see usp_Property_ApplyDeedVerificationOutcome's
    -- own header comment). Editing ordinary property fields must not let a
    -- Disapproved listing quietly escape back into the review workflow as
    -- if the problem had been resolved - only an explicit new
    -- verification run (SellerDeedVerificationSection's "Replace /
    -- Re-verify Deed", which calls usp_Property_ApplyDeedVerificationOutcome
    -- directly and is completely independent of this procedure) is allowed
    -- to move it out of Disapproved. Every other status still resets to
    -- 'Pending' on edit exactly as before (Approved -> Pending is the
    -- existing, intended re-review trigger; Pending/Flagged/Rejected stay
    -- Pending).
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

/*------------------------------------------------------------------------------
  usp_Property_Withdraw   ->  POST /api/properties/{id}/withdraw
  Phase F (Property Withdrawal / Soft Delete). Seller-initiated "Delete" no
  longer attempts a physical DELETE FROM dbo.Property (see usp_Property_Delete's
  own header note below) - it withdraws the listing instead: Status is set to
  'Withdrawn' and every child/audit record (DeedVerification, FraudCheck,
  RiskReport, AdminAction, Notification, PropertyImage) is left completely
  untouched. This is a listing lifecycle change, not a fraud verdict and not a
  deletion - the database row remains for auditability, and any Government
  Deed Verification / legacy fraud engine results attached to it stay valid.

  Seller-owned only (no Admin path here - Admin's hard-delete/cleanup
  procedure, usp_Property_Delete, is unchanged and kept separate).

  Allowed source states: Pending, Approved. A Flagged property is still
  mid-review (the seller should wait for/respond to that outcome first); a
  Rejected property already has a terminal admin decision; a Withdrawn
  property is already withdrawn. Each disallowed transition gets its own
  clear RAISERROR rather than a silent no-op, matching this procedure's
  neighbours (usp_Property_Update/usp_Property_Delete above).
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_Withdraw
    @PropertyID INT,
    @SellerID   INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentStatus VARCHAR(20), @Title NVARCHAR(200);
    SELECT @CurrentStatus = Status, @Title = Title
    FROM dbo.Property
    WHERE PropertyID = @PropertyID AND SellerID = @SellerID;

    IF @CurrentStatus IS NULL
    BEGIN
        RAISERROR (N'Property not found, or it does not belong to this seller.', 16, 1);
        RETURN -1;
    END

    IF @CurrentStatus = 'Withdrawn'
    BEGIN
        RAISERROR (N'This listing has already been withdrawn.', 16, 1);
        RETURN -2;
    END

    IF @CurrentStatus = 'Flagged'
    BEGIN
        RAISERROR (N'This listing is currently under fraud review and cannot be withdrawn yet. Please wait for the review to complete.', 16, 1);
        RETURN -3;
    END

    IF @CurrentStatus = 'Rejected'
    BEGIN
        RAISERROR (N'This listing was already rejected by an administrator and does not need to be withdrawn.', 16, 1);
        RETURN -4;
    END

    -- Only Pending and Approved reach here.
    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Property SET Status = 'Withdrawn' WHERE PropertyID = @PropertyID;

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID,
                N'Your listing "' + @Title + N'" has been withdrawn and is no longer visible to buyers. Its verification and audit history has been preserved.',
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

/*------------------------------------------------------------------------------
  usp_Property_Delete   ->  DELETE /api/properties/{id}
  Cascades to images, fraud checks, risk reports, saved items and reports.

  BUG FIX NOTE (seller delete silently/confusingly failing): Module5B
  (database/Module5B_DeedVerification.sql) added dbo.DeedVerification with
  FK_DeedVerification_Property ON DELETE NO ACTION - deliberately, per
  that script's own header comment: "Verification history is audit/history
  data; a property deletion must never silently cascade-delete the
  evidence of what was verified about it." DeedVerification.PropertyID is
  also NOT NULL (unlike AdminAction.PropertyID/Notification.RelatedPropertyID,
  which this procedure already detaches by setting them NULL below), so
  the same "null out the FK" trick cannot apply here without contradicting
  that column's own design. Before this fix, attempting DELETE FROM
  dbo.Property for any property with at least one persisted deed
  verification raised an unhandled FK-constraint SqlException (error 547)
  that surfaced as a confusing raw SQL error - the delete never happened
  and nothing about the failure was clear to the seller.
  This fix does NOT delete, orphan, or cascade-delete any DeedVerification/
  DeedVerificationField/DeedVerificationReason row - that audit trail is
  fully preserved. It only turns the previously-confusing FK violation
  into the same clear, friendly RAISERROR pattern already used for the
  ownership check immediately above it. A property with recorded deed
  verification history is therefore not deletable through this procedure
  at all yet (by Seller OR Admin) until a deliberate retention/soft-delete
  decision is made - see the chat report this fix shipped with.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Property_Delete
    @PropertyID INT,
    @UserID     INT     -- the seller who owns it, or an admin
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IsOwnerOrAdmin BIT = 0;

    IF EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID AND SellerID = @UserID)
        SET @IsOwnerOrAdmin = 1;
    ELSE IF EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @UserID AND Role = 'Admin')
        SET @IsOwnerOrAdmin = 1;

    IF @IsOwnerOrAdmin = 0
    BEGIN
        RAISERROR (N'Not authorised to delete this property.', 16, 1);
        RETURN -1;
    END

    IF EXISTS (SELECT 1 FROM dbo.DeedVerification WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'This property has a recorded Government Deed Verification and cannot be permanently deleted. Contact an administrator if this listing needs to be removed.', 16, 1);
        RETURN -2;
    END

    -- AdminAction rows reference the property with NO ACTION, so detach them first
    UPDATE dbo.AdminAction SET PropertyID = NULL WHERE PropertyID = @PropertyID;
    UPDATE dbo.Notification SET RelatedPropertyID = NULL WHERE RelatedPropertyID = @PropertyID;

    DELETE FROM dbo.Property WHERE PropertyID = @PropertyID;

    SELECT @@ROWCOUNT AS RowsDeleted;
    RETURN 0;
END;
GO


/*==============================================================================
  D. FRAUD DETECTION ENGINE   ->  FraudDetectionService + RiskReportService
  ------------------------------------------------------------------------------
  The 8 points:
     1. Price Anomaly          2. Duplicate Image      3. NIC Verification
     4. Deed Reference Dup.    5. Seller History       6. Location Validation
     7. Missing Information    8. Combined Risk Score  (usp_Risk_GenerateReport)
==============================================================================*/

CREATE OR ALTER PROCEDURE dbo.usp_Fraud_AnalyseProperty
    @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SellerID      INT,
            @Title         NVARCHAR(200),
            @Description   NVARCHAR(MAX),
            @Location      NVARCHAR(255),
            @District      NVARCHAR(100),
            @Latitude      DECIMAL(9,6),
            @Longitude     DECIMAL(9,6),
            @Size          FLOAT,
            @Price         DECIMAL(14,2),
            @PricePerPerch DECIMAL(14,2),
            @DeedReference VARCHAR(100);

    SELECT @SellerID      = SellerID,
           @Title         = Title,
           @Description   = Description,
           @Location      = Location,
           @District      = District,
           @Latitude      = Latitude,
           @Longitude     = Longitude,
           @Size          = Size,
           @Price         = Price,
           @PricePerPerch = PricePerPerch,
           @DeedReference = DeedReference
    FROM dbo.Property
    WHERE PropertyID = @PropertyID;

    IF @SellerID IS NULL
    BEGIN
        RAISERROR (N'Property not found - fraud analysis aborted.', 16, 1);
        RETURN -1;
    END

    DECLARE @PriceCheck         BIT = 0,
            @DuplicateCheck     BIT = 0,
            @NICCheck           BIT = 0,
            @DeedCheck          BIT = 0,
            @SellerHistoryCheck BIT = 0,
            @LocationCheck      BIT = 0,
            @MissingInfoCheck   BIT = 0;

    /*--------------------------------------------------------------------------
      CHECK 1 - PRICE ANOMALY
      Fires when the price per perch is more than 40% below the benchmark.
      Benchmark = the district reference rate; if the district has no reference
      row, fall back to the median price per perch of approved listings there.
    --------------------------------------------------------------------------*/
    DECLARE @Threshold   DECIMAL(9,4) =
            ISNULL((SELECT Threshold FROM dbo.FraudRuleWeight WHERE RuleCode = 'PRICE_ANOMALY'), 0.40);
    DECLARE @Benchmark   DECIMAL(14,2) =
            (SELECT MarketPricePerPerch FROM dbo.PriceBenchmark WHERE District = @District);

    IF @Benchmark IS NULL
        SELECT @Benchmark = AVG(PricePerPerch)
        FROM dbo.Property
        WHERE District = @District
          AND Status = 'Approved'
          AND PropertyID <> @PropertyID;

    IF @Benchmark IS NOT NULL AND @PricePerPerch IS NOT NULL
       AND @PricePerPerch < @Benchmark * (1 - @Threshold)
        SET @PriceCheck = 1;

    /*--------------------------------------------------------------------------
      CHECK 2 - DUPLICATE IMAGE
      Fires when any image hash on this listing already exists on another
      property. LIMITATION (Chapter 3.3): only images inside LandGuard are
      compared; images copied from external sites cannot be detected in v1.
    --------------------------------------------------------------------------*/
    IF EXISTS (
        SELECT 1
        FROM dbo.PropertyImage AS mine
        INNER JOIN dbo.PropertyImage AS other
                ON other.ImageHash = mine.ImageHash
               AND other.PropertyID <> mine.PropertyID
        WHERE mine.PropertyID = @PropertyID
          AND mine.ImageHash IS NOT NULL
    )
        SET @DuplicateCheck = 1;

    /*--------------------------------------------------------------------------
      CHECK 3 - SELLER NIC VERIFICATION  (FR02)
      Fires when the NIC is missing, malformed, not marked verified, or is
      shared with another account (the impersonation pattern from 2.5.2).
    --------------------------------------------------------------------------*/
    DECLARE @SellerNIC      VARCHAR(20),
            @SellerVerified BIT,
            @SellerActive   BIT;

    SELECT @SellerNIC      = NIC,
           @SellerVerified = NICVerified,
           @SellerActive   = IsActive
    FROM dbo.Users WHERE UserID = @SellerID;

    IF @SellerNIC IS NULL
       OR dbo.fn_IsValidNIC(@SellerNIC) = 0
       OR @SellerVerified = 0
       OR @SellerActive = 0
       OR EXISTS (SELECT 1 FROM dbo.Users
                   WHERE NIC = @SellerNIC AND UserID <> @SellerID)
        SET @NICCheck = 1;

    /*--------------------------------------------------------------------------
      CHECK 4 - DEED REFERENCE DUPLICATE
      Fires when the same deed reference is already used by a live listing -
      the "one plot sold twice" pattern (Sunday Times, Table 2.5.2).
    --------------------------------------------------------------------------*/
    IF @DeedReference IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.Property
                    WHERE DeedReference = @DeedReference
                      AND PropertyID <> @PropertyID
                      AND Status IN ('Pending','Approved','Flagged'))
        SET @DeedCheck = 1;

    /*--------------------------------------------------------------------------
      CHECK 5 - SELLER HISTORY
      Fires when the seller already has 2+ rejected listings or 2+ resolved
      suspicious reports against their listings.
    --------------------------------------------------------------------------*/
    DECLARE @HistoryThreshold INT =
            CAST(ISNULL((SELECT Threshold FROM dbo.FraudRuleWeight
                          WHERE RuleCode = 'SELLER_HISTORY'), 2) AS INT);

    DECLARE @BadHistory INT =
    (
        SELECT
            (SELECT COUNT(*) FROM dbo.Property
              WHERE SellerID = @SellerID AND Status = 'Rejected' AND PropertyID <> @PropertyID)
          + (SELECT COUNT(*) FROM dbo.SuspiciousReport AS sr
             INNER JOIN dbo.Property AS p ON p.PropertyID = sr.PropertyID
              WHERE p.SellerID = @SellerID AND sr.Status = 'Resolved')
    );

    IF @BadHistory >= @HistoryThreshold
        SET @SellerHistoryCheck = 1;

    /*--------------------------------------------------------------------------
      CHECK 6 - LOCATION VALIDATION (Nominatim API)
      The API layer geocodes the typed location and writes Latitude/Longitude
      back before analysis. Missing coordinates, or coordinates outside the
      Sri Lankan bounding box (5.9-9.9 N, 79.6-81.9 E), fire this rule.
    --------------------------------------------------------------------------*/
    IF @Latitude IS NULL OR @Longitude IS NULL
       OR @Latitude  NOT BETWEEN 5.900000 AND 9.900000
       OR @Longitude NOT BETWEEN 79.600000 AND 81.900000
        SET @LocationCheck = 1;

    /*--------------------------------------------------------------------------
      CHECK 7 - MISSING INFORMATION
      Fires when any mandatory listing detail is absent or too thin.

      PHASE E NOTE (Supporting Risk Indicator Refactor): "deed present" no
      longer means only Property.DeedReference (an optional free-text
      field a seller may leave blank even after genuinely uploading and
      verifying a deed - see PropertyFormPage/DeedVerificationController).
      dbo.DeedVerification.SellerDocumentReference is the actual uploaded-
      document persistence Government Deed Verification writes once OCR +
      comparison + classification succeed for this property (Phase D) - a
      row with a non-null SellerDocumentReference here means a real deed
      document exists for this property, regardless of whether the
      optional DeedReference text was ever filled in. Deed presence is
      therefore now EITHER signal, not just the text field; an empty
      DeedReference alone must not fire this rule when a seller deed
      document has actually been uploaded and verified. Property.DeedReference
      itself is unchanged and remains optional - this only changes how its
      absence is interpreted here.
    --------------------------------------------------------------------------*/
    DECLARE @ImageCount INT =
            (SELECT COUNT(*) FROM dbo.PropertyImage WHERE PropertyID = @PropertyID);
    DECLARE @SellerPhone VARCHAR(20) =
            (SELECT Phone FROM dbo.Users WHERE UserID = @SellerID);
    DECLARE @HasSellerDeedDocument BIT =
            CASE WHEN EXISTS (
                SELECT 1 FROM dbo.DeedVerification
                WHERE PropertyID = @PropertyID AND SellerDocumentReference IS NOT NULL
            ) THEN 1 ELSE 0 END;

    IF (@DeedReference IS NULL AND @HasSellerDeedDocument = 0)
       OR @Description IS NULL OR LEN(LTRIM(RTRIM(@Description))) < 30
       OR @ImageCount = 0
       OR @District IS NULL OR LTRIM(RTRIM(@District)) = ''
       OR @SellerPhone IS NULL OR LTRIM(RTRIM(@SellerPhone)) = ''
        SET @MissingInfoCheck = 1;

    /*--------------------------------------------------------------------------
      Persist the run, then generate the risk report (point 8).
    --------------------------------------------------------------------------*/
    DECLARE @FraudCheckID INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.FraudCheck
            (PropertyID, PriceCheck, DuplicateCheck, NICCheck, DeedCheck,
             SellerHistoryCheck, LocationCheck, MissingInfoCheck, FraudStatus)
        VALUES
            (@PropertyID, @PriceCheck, @DuplicateCheck, @NICCheck, @DeedCheck,
             @SellerHistoryCheck, @LocationCheck, @MissingInfoCheck, 'Clean');

        SET @FraudCheckID = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    EXEC dbo.usp_Risk_GenerateReport @FraudCheckID = @FraudCheckID;

    SELECT @FraudCheckID AS FraudCheckID;
    RETURN 0;
END;
GO


/*------------------------------------------------------------------------------
  usp_Risk_GenerateReport   -  POINT 8 of the engine (RiskReportService)
  Sums the weights of every rule that fired, bands the total into
  Low / Medium / High, writes a human-readable summary, and notifies the
  seller. Does NOT change dbo.Property.Status - see the PHASE C NOTE below.

  PHASE B NOTE (Government Deed Verification module):
  RiskScore / RiskLevel / FraudStatus / Summary, as written by this
  procedure, are SUPPORTING FRAUD INDICATORS ONLY. They are no longer
  authoritative for deed authenticity - GovernmentDeedVerificationService's
  Verified/Fraudulent/PriceAnomaly/Unverified/UnverifiedCancelled
  classification (Application layer, C#) is authoritative for that.
  Nothing here should be read, displayed, or reasoned about as "proof" a
  deed is Clean/Suspicious/Fraudulent - that is legacy terminology kept
  only for backward-compatible history (existing FraudCheck/RiskReport
  rows, usp_Fraud_GetHistory, the existing fraud report endpoints).

  PHASE C NOTE (Property Status Workflow Refactor) - READ BEFORE EDITING:
  This procedure USED TO auto-transition dbo.Property.Status here
  (Low -> Approved, else -> Flagged) immediately after every fraud
  analysis run - see git history / FraudEngine.md for the old behavior.
  That transition has been REMOVED. A property now stays in whatever
  status it already had (normally 'Pending', set by
  usp_Property_Create/usp_Property_Update) after this procedure runs,
  regardless of RiskLevel. The only two things that now change
  dbo.Property.Status are usp_Admin_ApproveProperty (-> 'Approved') and
  usp_Admin_RejectProperty (-> 'Rejected'), both already wired to
  POST /api/admin/properties/{id}/approve|reject (Phase B2 - see
  AdminController.cs). This is a deliberate architectural decision:
  RiskScore/RiskLevel/FraudStatus are supporting risk indicators for the
  admin to review, not an automatic publication mechanism. Do not
  reintroduce a Property.Status write here without a new, deliberately
  chosen decision - see database/Database/Docs/FraudEngine.md.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Risk_GenerateReport
    @FraudCheckID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PropertyID INT, @SellerID INT, @Title NVARCHAR(200);

    SELECT @PropertyID = fc.PropertyID,
           @SellerID   = p.SellerID,
           @Title      = p.Title
    FROM dbo.FraudCheck AS fc
    INNER JOIN dbo.Property AS p ON p.PropertyID = fc.PropertyID
    WHERE fc.FraudCheckID = @FraudCheckID;

    IF @PropertyID IS NULL
    BEGIN
        RAISERROR (N'Fraud check record not found.', 16, 1);
        RETURN -1;
    END

    /* Weighted score - weights come from dbo.FraudRuleWeight and total 100 */
    DECLARE @RiskScore INT;

    SELECT @RiskScore =
          (CASE WHEN fc.PriceCheck         = 1 THEN dbo.fn_GetRuleWeight('PRICE_ANOMALY')    ELSE 0 END)
        + (CASE WHEN fc.DuplicateCheck     = 1 THEN dbo.fn_GetRuleWeight('IMAGE_DUPLICATE')  ELSE 0 END)
        + (CASE WHEN fc.NICCheck           = 1 THEN dbo.fn_GetRuleWeight('NIC_VERIFICATION') ELSE 0 END)
        + (CASE WHEN fc.DeedCheck          = 1 THEN dbo.fn_GetRuleWeight('DEED_DUPLICATE')   ELSE 0 END)
        + (CASE WHEN fc.SellerHistoryCheck = 1 THEN dbo.fn_GetRuleWeight('SELLER_HISTORY')   ELSE 0 END)
        + (CASE WHEN fc.LocationCheck      = 1 THEN dbo.fn_GetRuleWeight('LOCATION_INVALID') ELSE 0 END)
        + (CASE WHEN fc.MissingInfoCheck   = 1 THEN dbo.fn_GetRuleWeight('MISSING_INFO')     ELSE 0 END)
    FROM dbo.FraudCheck AS fc
    WHERE fc.FraudCheckID = @FraudCheckID;

    IF @RiskScore > 100 SET @RiskScore = 100;
    IF @RiskScore < 0   SET @RiskScore = 0;

    DECLARE @RiskLevel   VARCHAR(20) = dbo.fn_RiskLevelFromScore(@RiskScore);
    DECLARE @FraudStatus VARCHAR(20) =
            CASE @RiskLevel WHEN 'Low' THEN 'Clean'
                            WHEN 'Medium' THEN 'Suspicious'
                            ELSE 'Fraudulent' END;

    /* Human-readable summary listing every rule that fired */
    DECLARE @Reasons NVARCHAR(MAX);

    SELECT @Reasons = STRING_AGG(
               CAST(N'- ' + w.RuleName + N' (+' + CAST(w.Weight AS NVARCHAR(10)) +
                    N'): ' + w.Description AS NVARCHAR(MAX)),
               CHAR(13) + CHAR(10))
           WITHIN GROUP (ORDER BY w.Weight DESC)
    FROM dbo.FraudCheck AS fc
    CROSS APPLY (VALUES
            ('PRICE_ANOMALY',    fc.PriceCheck),
            ('IMAGE_DUPLICATE',  fc.DuplicateCheck),
            ('NIC_VERIFICATION', fc.NICCheck),
            ('DEED_DUPLICATE',   fc.DeedCheck),
            ('SELLER_HISTORY',   fc.SellerHistoryCheck),
            ('LOCATION_INVALID', fc.LocationCheck),
            ('MISSING_INFO',     fc.MissingInfoCheck)
        ) AS x(RuleCode, Triggered)
    INNER JOIN dbo.FraudRuleWeight AS w ON w.RuleCode = x.RuleCode
    WHERE fc.FraudCheckID = @FraudCheckID AND x.Triggered = 1;

    -- PHASE E NOTE: wording only (no logic/weight/threshold change) - these
    -- are supporting LISTING RISK indicators, not a deed-authenticity
    -- verdict, so the stored summary/notification text avoids "fraud"/
    -- "fraudulent" language that could be misread as one. Government Deed
    -- Verification (Application layer, C#) remains the authoritative deed
    -- comparison; this procedure never claims to be that.
    DECLARE @Summary NVARCHAR(MAX) =
        N'Risk score ' + CAST(@RiskScore AS NVARCHAR(10)) + N'/100 (' + @RiskLevel + N' risk). ' +
        CASE WHEN @Reasons IS NULL OR LEN(@Reasons) = 0
             THEN N'All 7 listing risk checks passed. No risk indicators were found.'
             ELSE N'The following listing risk indicators were detected:' + CHAR(13) + CHAR(10) + @Reasons
        END;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.FraudCheck
        SET FraudStatus = @FraudStatus
        WHERE FraudCheckID = @FraudCheckID;

        IF EXISTS (SELECT 1 FROM dbo.RiskReport WHERE FraudCheckID = @FraudCheckID)
            UPDATE dbo.RiskReport
            SET RiskScore = @RiskScore, RiskLevel = @RiskLevel,
                Summary = @Summary, GeneratedDate = SYSDATETIME()
            WHERE FraudCheckID = @FraudCheckID;
        ELSE
            INSERT INTO dbo.RiskReport (FraudCheckID, RiskScore, RiskLevel, Summary)
            VALUES (@FraudCheckID, @RiskScore, @RiskLevel, @Summary);

        /* PHASE C: dbo.Property.Status is deliberately NOT written here - see
           the PHASE C NOTE above this procedure. RiskLevel/RiskScore are
           supporting indicators only; Property.Status now changes
           exclusively via usp_Admin_ApproveProperty/usp_Admin_RejectProperty. */

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID,
                N'Risk analysis completed for "' + @Title + N'". Listing risk level: ' + @RiskLevel +
                N' (' + CAST(@RiskScore AS NVARCHAR(10)) + N'/100). Your listing is pending admin review.',
                @PropertyID);

        /* High risk raises an alert for every admin */
        IF @RiskLevel = 'High'
            INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
            SELECT UserID,
                   N'Potential listing concerns detected: "' + @Title + N'" scored ' +
                   CAST(@RiskScore AS NVARCHAR(10)) + N'/100 (High risk band). Review may be required.',
                   @PropertyID
            FROM dbo.Users WHERE Role = 'Admin' AND IsActive = 1;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT ReportID, FraudCheckID, RiskScore, RiskLevel, Summary, GeneratedDate
    FROM dbo.RiskReport WHERE FraudCheckID = @FraudCheckID;

    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_Fraud_ReanalyseAll - re-scores every listing after a weight/threshold
  change. This is the operational half of the Chapter 3.3 mitigation
  "review and adjust rule thresholds periodically".
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Fraud_ReanalyseAll
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PropertyID INT;
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT PropertyID FROM dbo.Property ORDER BY PropertyID;

    OPEN cur;
    FETCH NEXT FROM cur INTO @PropertyID;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @PropertyID;
        FETCH NEXT FROM cur INTO @PropertyID;
    END

    CLOSE cur;
    DEALLOCATE cur;

    PRINT '>> All properties re-analysed.';
END;
GO


/*==============================================================================
  E. BUYER FEATURES
==============================================================================*/

/*------------------------------------------------------------------------------
  usp_SuspiciousReport_Create   ->  POST /api/reports    (FR12)
  Files a report and notifies every admin.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_SuspiciousReport_Create
    @BuyerID     INT,
    @PropertyID  INT,
    @Reason      NVARCHAR(255),
    @Description NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @BuyerID AND IsActive = 1)
    BEGIN
        RAISERROR (N'Reporting user not found or inactive.', 16, 1);
        RETURN -1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -2;
    END

    IF EXISTS (SELECT 1 FROM dbo.SuspiciousReport
                WHERE BuyerID = @BuyerID AND PropertyID = @PropertyID AND Reason = @Reason)
    BEGIN
        RAISERROR (N'You have already reported this listing for that reason.', 16, 1);
        RETURN -3;
    END

    DECLARE @ReportID INT, @Title NVARCHAR(200);
    SELECT @Title = Title FROM dbo.Property WHERE PropertyID = @PropertyID;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.SuspiciousReport (BuyerID, PropertyID, Reason, Description)
        VALUES (@BuyerID, @PropertyID, @Reason, @Description);

        SET @ReportID = SCOPE_IDENTITY();

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        SELECT UserID,
               N'New suspicious listing report on "' + @Title + N'". Reason: ' + @Reason,
               @PropertyID
        FROM dbo.Users WHERE Role = 'Admin' AND IsActive = 1;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @ReportID AS SuspiciousReportID;
    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  Saved properties (FR07)
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_SavedProperty_Add
    @BuyerID INT, @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.SavedProperty WHERE BuyerID = @BuyerID AND PropertyID = @PropertyID)
    BEGIN
        SELECT SavedPropertyID FROM dbo.SavedProperty
        WHERE BuyerID = @BuyerID AND PropertyID = @PropertyID;
        RETURN 0;
    END

    INSERT INTO dbo.SavedProperty (BuyerID, PropertyID) VALUES (@BuyerID, @PropertyID);
    SELECT SCOPE_IDENTITY() AS SavedPropertyID;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_SavedProperty_Remove
    @BuyerID INT, @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.SavedProperty WHERE BuyerID = @BuyerID AND PropertyID = @PropertyID;
    SELECT @@ROWCOUNT AS RowsDeleted;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_SavedProperty_GetByBuyer
    @BuyerID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.vw_BuyerSavedProperty WHERE BuyerID = @BuyerID ORDER BY SavedDate DESC;
END;
GO


/*==============================================================================
  F. ADMIN FEATURES   ->  AdminController
==============================================================================*/

/*------------------------------------------------------------------------------
  usp_Admin_GetFlagged   ->  GET /api/admin/flagged
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_GetFlagged
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.vw_FlaggedProperty ORDER BY RiskScore DESC, UploadDate ASC;
END;
GO

/*------------------------------------------------------------------------------
  usp_Admin_ApproveProperty   ->  PUT /api/admin/approve/{id}
  The manual appeal path for legitimate low-price / distress-sale listings
  (Chapter 3.3 mitigation).
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

/*------------------------------------------------------------------------------
  usp_Admin_RejectProperty   ->  PUT /api/admin/reject/{id}
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_RejectProperty
    @AdminID    INT,
    @PropertyID INT,
    @Remarks    NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @AdminID AND Role = 'Admin' AND IsActive = 1)
    BEGIN
        RAISERROR (N'Only an active administrator can reject listings.', 16, 1);
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

    -- Phase F (Property Withdrawal): see the identical guard in
    -- usp_Admin_ApproveProperty above - a Withdrawn listing must not be
    -- moderated through the normal Approve/Reject flow.
    IF @CurrentStatus = 'Withdrawn'
    BEGIN
        RAISERROR (N'This listing has been withdrawn by the seller and is no longer part of the active review queue.', 16, 1);
        RETURN -3;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Property SET Status = 'Rejected' WHERE PropertyID = @PropertyID;

        INSERT INTO dbo.AdminAction (AdminID, ActionType, PropertyID, Remarks)
        VALUES (@AdminID, 'RejectListing', @PropertyID, @Remarks);

        UPDATE dbo.SuspiciousReport
        SET Status = 'Resolved'
        WHERE PropertyID = @PropertyID AND Status <> 'Resolved';

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID,
                N'Your listing "' + @Title + N'" was rejected. Reason: ' +
                ISNULL(@Remarks, N'Failed fraud verification.') +
                N' You may correct the details and resubmit.',
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

/*------------------------------------------------------------------------------
  usp_Admin_SetUserActive - suspend or reactivate an account
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_SetUserActive
    @AdminID      INT,
    @TargetUserID INT,
    @IsActive     BIT,
    @Remarks      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @AdminID AND Role = 'Admin' AND IsActive = 1)
    BEGIN
        RAISERROR (N'Only an active administrator can change account status.', 16, 1);
        RETURN -1;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Users SET IsActive = @IsActive WHERE UserID = @TargetUserID;

        INSERT INTO dbo.AdminAction (AdminID, ActionType, TargetUserID, Remarks)
        VALUES (@AdminID,
                CASE WHEN @IsActive = 1 THEN 'ReactivateUser' ELSE 'SuspendUser' END,
                @TargetUserID, @Remarks);

        INSERT INTO dbo.Notification (UserID, Message)
        VALUES (@TargetUserID,
                CASE WHEN @IsActive = 1
                     THEN N'Your LandGuard account has been reactivated.'
                     ELSE N'Your LandGuard account has been suspended. Reason: ' +
                          ISNULL(@Remarks, N'Repeated fraudulent activity.') END);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT UserID, Name, Email, Role, IsActive FROM dbo.Users WHERE UserID = @TargetUserID;
    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_Admin_ResolveReport - close a buyer's suspicious report and notify them
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_ResolveReport
    @AdminID  INT,
    @ReportID INT,
    @Outcome  VARCHAR(20),          -- 'Resolved' or 'Under Review'
    @Remarks  NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Outcome NOT IN ('Resolved','Under Review')
    BEGIN
        RAISERROR (N'Outcome must be Resolved or Under Review.', 16, 1);
        RETURN -1;
    END

    DECLARE @BuyerID INT, @PropertyID INT;
    SELECT @BuyerID = BuyerID, @PropertyID = PropertyID
    FROM dbo.SuspiciousReport WHERE SuspiciousReportID = @ReportID;

    IF @BuyerID IS NULL
    BEGIN
        RAISERROR (N'Report not found.', 16, 1);
        RETURN -2;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.SuspiciousReport SET Status = @Outcome WHERE SuspiciousReportID = @ReportID;

        INSERT INTO dbo.AdminAction (AdminID, ActionType, PropertyID, ReportID, Remarks)
        VALUES (@AdminID, 'ResolveReport', @PropertyID, @ReportID, @Remarks);

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@BuyerID,
                N'Your report has been reviewed. Outcome: ' + @Outcome +
                ISNULL(N'. ' + @Remarks, N''),
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

/*------------------------------------------------------------------------------
  usp_Admin_VerifyNIC - manual seller identity confirmation (FR02 / FR09)
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_VerifyNIC
    @AdminID      INT,
    @TargetUserID INT,
    @Remarks      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users SET NICVerified = 1 WHERE UserID = @TargetUserID;

    -- Kept in lockstep with IdentityStatus (the other authoritative "seller
    -- is verified" write, set by usp_User_SetIdentityStatus for the
    -- automated Government Identity Registry path) so a manual Admin
    -- verification cannot leave the two contradicting each other -
    -- property listing gates on IdentityStatus, not NICVerified, so
    -- without this a manual verification would silently fail to unlock
    -- listing. Only touches Seller rows, matching IdentityStatus's own
    -- Seller-only scope.
    UPDATE dbo.Users SET IdentityStatus = 'Verified'
    WHERE UserID = @TargetUserID AND Role = 'Seller';

    INSERT INTO dbo.AdminAction (AdminID, ActionType, TargetUserID, Remarks)
    VALUES (@AdminID, 'VerifyNIC', @TargetUserID, @Remarks);

    INSERT INTO dbo.Notification (UserID, Message)
    VALUES (@TargetUserID, N'Your NIC has been verified. You can now list properties as a verified seller.');
END;
GO

/*------------------------------------------------------------------------------
  usp_Admin_GetDashboard - statistics + rule trigger frequency + review queue
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_GetDashboard
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.vw_FraudStatistics;
    SELECT * FROM dbo.vw_RuleTriggerFrequency ORDER BY TimesTriggered DESC;
    SELECT TOP (20) * FROM dbo.vw_FlaggedProperty ORDER BY RiskScore DESC;
END;
GO

/*------------------------------------------------------------------------------
  usp_Admin_UpdateRuleWeight - retune the engine without redeploying the API
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_UpdateRuleWeight
    @RuleCode   VARCHAR(30),
    @Weight     INT           = NULL,
    @Threshold  DECIMAL(9,4)  = NULL,
    @IsEnabled  BIT           = NULL,
    @Reanalyse  BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.FraudRuleWeight WHERE RuleCode = @RuleCode)
    BEGIN
        RAISERROR (N'Unknown rule code.', 16, 1);
        RETURN -1;
    END

    UPDATE dbo.FraudRuleWeight
    SET Weight    = ISNULL(@Weight,    Weight),
        Threshold = ISNULL(@Threshold, Threshold),
        IsEnabled = ISNULL(@IsEnabled, IsEnabled)
    WHERE RuleCode = @RuleCode;

    DECLARE @Total INT = (SELECT SUM(CASE WHEN IsEnabled = 1 THEN Weight ELSE 0 END)
                          FROM dbo.FraudRuleWeight);

    IF @Total > 100
        PRINT '>> WARNING: enabled rule weights now total ' + CAST(@Total AS VARCHAR(10)) +
              '. Scores will be capped at 100.';

    IF @Reanalyse = 1
        EXEC dbo.usp_Fraud_ReanalyseAll;

    SELECT * FROM dbo.FraudRuleWeight ORDER BY Weight DESC;
END;
GO


/*==============================================================================
  G. NOTIFICATIONS & PODCASTS
==============================================================================*/

CREATE OR ALTER PROCEDURE dbo.usp_Notification_GetByUser
    @UserID     INT,
    @UnreadOnly BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT NotificationID, Message, NotificationDate, Status, RelatedPropertyID
    FROM dbo.Notification
    WHERE UserID = @UserID
      AND (@UnreadOnly = 0 OR Status = 'Unread')
    ORDER BY NotificationDate DESC, NotificationID DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Notification_MarkRead
    @UserID         INT,
    @NotificationID INT = NULL     -- NULL marks every notification as read
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Notification
    SET Status = 'Read'
    WHERE UserID = @UserID
      AND (@NotificationID IS NULL OR NotificationID = @NotificationID);
    SELECT @@ROWCOUNT AS RowsUpdated;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Podcast_Add
    @AdminID     INT,
    @Title       NVARCHAR(200),
    @Language    VARCHAR(50),
    @AudioURL    NVARCHAR(500),
    @Description NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @AdminID AND Role = 'Admin')
    BEGIN
        RAISERROR (N'Only an administrator can upload fraud awareness content.', 16, 1);
        RETURN -1;
    END

    INSERT INTO dbo.Podcast (AdminID, Title, Language, Description, AudioURL)
    VALUES (@AdminID, @Title, @Language, @Description, @AudioURL);

    SELECT SCOPE_IDENTITY() AS PodcastID;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Podcast_GetAll
    @Language VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PodcastID, p.Title, p.Language, p.Description, p.AudioURL, p.UploadDate,
           u.Name AS UploadedBy
    FROM dbo.Podcast AS p
    INNER JOIN dbo.Users AS u ON u.UserID = p.AdminID
    WHERE (@Language IS NULL OR p.Language = @Language)
    ORDER BY p.UploadDate DESC;
END;
GO

PRINT '>> 3 functions and 27 stored procedures created.';
GO
