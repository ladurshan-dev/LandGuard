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
    SELECT UserID, Name, Email, Role, NIC, Phone, NICVerified, IsActive, CreatedAt
    FROM dbo.Users WHERE UserID = @UserID;
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
    @DeedReference  VARCHAR(100)    = NULL,
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

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.Property
            (SellerID, Title, Description, Location, District, Latitude, Longitude,
             Size, Price, DeedReference, Status)
        VALUES
            (@SellerID, @Title, @Description, @Location, @District, @Latitude, @Longitude,
             @Size, @Price, @DeedReference, 'Pending');

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
    @SortBy      VARCHAR(20)   = 'Newest', -- Newest | PriceAsc | PriceDesc | RiskAsc
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
    @DeedReference  VARCHAR(100)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property
                    WHERE PropertyID = @PropertyID AND SellerID = @SellerID)
    BEGIN
        RAISERROR (N'Property not found, or it does not belong to this seller.', 16, 1);
        RETURN -1;
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
        Status        = 'Pending'
    WHERE PropertyID = @PropertyID;

    EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @PropertyID;

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @PropertyID;
    RETURN 0;
END;
GO

/*------------------------------------------------------------------------------
  usp_Property_Delete   ->  DELETE /api/properties/{id}
  Cascades to images, fraud checks, risk reports, saved items and reports.
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
    --------------------------------------------------------------------------*/
    DECLARE @ImageCount INT =
            (SELECT COUNT(*) FROM dbo.PropertyImage WHERE PropertyID = @PropertyID);
    DECLARE @SellerPhone VARCHAR(20) =
            (SELECT Phone FROM dbo.Users WHERE UserID = @SellerID);

    IF @DeedReference IS NULL
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
  Low / Medium / High, writes a human-readable summary, updates the listing
  status and notifies the seller.
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

    DECLARE @Summary NVARCHAR(MAX) =
        N'Risk score ' + CAST(@RiskScore AS NVARCHAR(10)) + N'/100 (' + @RiskLevel + N' risk). ' +
        CASE WHEN @Reasons IS NULL OR LEN(@Reasons) = 0
             THEN N'All 7 fraud detection rules passed. No fraud indicators were found.'
             ELSE N'The following fraud indicators were detected:' + CHAR(13) + CHAR(10) + @Reasons
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

        /* Low risk publishes automatically; anything higher waits for an admin */
        UPDATE dbo.Property
        SET Status = CASE WHEN @RiskLevel = 'Low' THEN 'Approved' ELSE 'Flagged' END
        WHERE PropertyID = @PropertyID
          AND Status IN ('Pending','Flagged','Approved');

        INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
        VALUES (@SellerID,
                N'Fraud analysis complete for "' + @Title + N'". Risk: ' + @RiskLevel +
                N' (' + CAST(@RiskScore AS NVARCHAR(10)) + N'/100). Status: ' +
                CASE WHEN @RiskLevel = 'Low' THEN N'Published.'
                     ELSE N'Sent to admin for review.' END,
                @PropertyID);

        /* High risk raises an alert for every admin */
        IF @RiskLevel = 'High'
            INSERT INTO dbo.Notification (UserID, Message, RelatedPropertyID)
            SELECT UserID,
                   N'HIGH RISK listing submitted: "' + @Title + N'" scored ' +
                   CAST(@RiskScore AS NVARCHAR(10)) + N'/100. Review required.',
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

    DECLARE @SellerID INT, @Title NVARCHAR(200);
    SELECT @SellerID = SellerID, @Title = Title FROM dbo.Property WHERE PropertyID = @PropertyID;

    IF @SellerID IS NULL
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -2;
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

    DECLARE @SellerID INT, @Title NVARCHAR(200);
    SELECT @SellerID = SellerID, @Title = Title FROM dbo.Property WHERE PropertyID = @PropertyID;

    IF @SellerID IS NULL
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -2;
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
