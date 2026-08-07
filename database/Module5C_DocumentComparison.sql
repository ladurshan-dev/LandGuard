/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : Module5C_DocumentComparison.sql
  Purpose : Adds durable storage for OCR-based deed comparison results
            (Module 5C) - a parent table (one row per comparison run), a
            child table (one row per compared field), a table type used to
            pass the field rows to the save procedure in a single round
            trip, and the two stored procedures Module 5C needs
            (usp_DocumentComparison_Save, usp_DocumentComparison_GetLatest).

  Context: Module 5B (OCR Integration) deliberately persists nothing to
  LandGuardDB - POST /api/ocr/extract returns extracted text/fields
  directly to the caller. Module 5C's brief asks for both a POST that
  "consumes the OCR results already produced" (so the caller supplies
  them - the extraction itself is not repeated here) and a GET that
  returns the latest comparison with no request body, which only makes
  sense if a comparison result is stored somewhere after POST runs it.
  Asked how this should be reconciled, the answer was: add one new,
  narrow, durable table (plus whatever supporting objects it needs) rather
  than an in-memory cache. This is therefore the first change in this
  project that adds a new TABLE (every prior additive script - Module 3's
  ChangePassword, Module 5A's FraudHistory - only added a stored procedure
  over tables that already existed).

  Design notes:
    - Header (DocumentComparison) + detail (DocumentComparisonField) rather
      than one very wide row: 10 compared fields x 5 attributes each
      (OCR value, DB value, matched, similarity, message) would mean 50+
      columns on a single row, which is both unreadable and inflexible if
      the set of compared fields ever changes. A parent/child pair with an
      IDENTITY join, one detail row per field, follows the same shape this
      schema already uses for Property/PropertyImage.
    - Only the LATEST comparison is read back by
      usp_DocumentComparison_GetLatest (matching the singular
      "GET /api/fraud/comparison/{propertyId}" endpoint) - every run is
      still kept (no DELETE/overwrite of prior rows), so a future
      "comparison history" endpoint (mirroring usp_Fraud_GetHistory) can
      be added later without any further schema change.
    - dbo.DocumentComparisonFieldType (a user-defined table type) lets the
      C# layer pass all of a run's field rows to
      usp_DocumentComparison_Save as a single table-valued parameter
      (Dapper's AsTableValuedParameter) instead of N separate INSERT
      round trips.

  Why this is a separate file instead of editing 01_Schema.sql /
  04_StoredProcedures.sql:
    Same reason as every prior additive script in this checkout (Module 3's
    Module3_ChangePassword.sql, Module 5A's Module5A_FraudHistory.sql) -
    this checkout does not contain the canonical Database/Scripts folder
    (it lives in the database owner's own checkout). Please fold the two
    CREATE TABLE statements into Database/Scripts/01_Schema.sql, the type
    into wherever that repository keeps user-defined types (or immediately
    before 04_StoredProcedures.sql if it keeps none yet), and the two
    procedures into Database/Scripts/04_StoredProcedures.sql (a new
    section, e.g. "Section G - Document Comparison") the next time that
    repository is updated.

  Nature of the change : ADDITIVE ONLY.
    - Two new tables, one new table type, two new stored procedures.
    - No ALTER TABLE against any existing table, no new column on
      dbo.Property/dbo.Users, no change to usp_Fraud_AnalyseProperty,
      usp_Risk_GenerateReport, or any existing view/procedure.
  Author  : LandGuard Module 5C (OCR-Based Fraud Comparison)
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  dbo.DocumentComparison - one row per comparison run (POST /api/fraud/compare).
------------------------------------------------------------------------------*/
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DocumentComparison' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DocumentComparison
    (
        ComparisonID            INT             IDENTITY(1,1)   NOT NULL,
        PropertyID              INT                             NOT NULL,
        ComparedByUserID        INT                             NOT NULL,
        DocumentReference       NVARCHAR(300)                       NULL,
        FieldsCompared          INT                             NOT NULL,
        FieldsMatched           INT                             NOT NULL,
        OverallMatchPercentage  DECIMAL(5,2)                    NOT NULL,
        Summary                 NVARCHAR(MAX)                       NULL,
        ComparisonDate          DATETIME2(0)                    NOT NULL,

        CONSTRAINT PK_DocumentComparison            PRIMARY KEY CLUSTERED (ComparisonID),
        CONSTRAINT FK_DocumentComparison_Property    FOREIGN KEY (PropertyID)
            REFERENCES dbo.Property (PropertyID) ON DELETE CASCADE,
        CONSTRAINT FK_DocumentComparison_ComparedBy  FOREIGN KEY (ComparedByUserID)
            REFERENCES dbo.Users (UserID) ON DELETE NO ACTION,
        CONSTRAINT CK_DocumentComparison_MatchPct    CHECK (OverallMatchPercentage BETWEEN 0 AND 100),
        CONSTRAINT CK_DocumentComparison_FieldCounts CHECK (FieldsMatched <= FieldsCompared AND FieldsMatched >= 0)
    );

    ALTER TABLE dbo.DocumentComparison
        ADD CONSTRAINT DF_DocumentComparison_Date DEFAULT (SYSDATETIME()) FOR ComparisonDate;

    CREATE INDEX IX_DocumentComparison_Property_Date
        ON dbo.DocumentComparison (PropertyID, ComparisonDate DESC);

    PRINT '>> dbo.DocumentComparison created (Module 5C).';
END
ELSE
BEGIN
    PRINT '>> dbo.DocumentComparison already exists - skipped.';
END
GO

/*------------------------------------------------------------------------------
  dbo.DocumentComparisonField - one row per compared field within a run.
------------------------------------------------------------------------------*/
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DocumentComparisonField' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DocumentComparisonField
    (
        ComparisonFieldID      INT             IDENTITY(1,1)   NOT NULL,
        ComparisonID           INT                             NOT NULL,
        FieldName               VARCHAR(50)                     NOT NULL,
        OcrValue                 NVARCHAR(500)                       NULL,
        DatabaseValue             NVARCHAR(500)                       NULL,
        Matched                   BIT                             NOT NULL,
        SimilarityPercentage      DECIMAL(5,2)                    NOT NULL,
        Message                   NVARCHAR(300)                       NULL,

        CONSTRAINT PK_DocumentComparisonField            PRIMARY KEY CLUSTERED (ComparisonFieldID),
        CONSTRAINT FK_DocumentComparisonField_Comparison FOREIGN KEY (ComparisonID)
            REFERENCES dbo.DocumentComparison (ComparisonID) ON DELETE CASCADE,
        CONSTRAINT CK_DocumentComparisonField_Similarity CHECK (SimilarityPercentage BETWEEN 0 AND 100)
    );

    CREATE INDEX IX_DocumentComparisonField_Comparison
        ON dbo.DocumentComparisonField (ComparisonID);

    PRINT '>> dbo.DocumentComparisonField created (Module 5C).';
END
ELSE
BEGIN
    PRINT '>> dbo.DocumentComparisonField already exists - skipped.';
END
GO

/*------------------------------------------------------------------------------
  dbo.DocumentComparisonFieldType - table type for usp_DocumentComparison_Save's
  @Fields parameter. Columns mirror dbo.DocumentComparisonField exactly (minus
  the identity/FK columns, which the procedure fills in itself).
------------------------------------------------------------------------------*/
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'DocumentComparisonFieldType' AND is_table_type = 1)
BEGIN
    CREATE TYPE dbo.DocumentComparisonFieldType AS TABLE
    (
        FieldName             VARCHAR(50)     NOT NULL,
        OcrValue                NVARCHAR(500)       NULL,
        DatabaseValue            NVARCHAR(500)       NULL,
        Matched                  BIT             NOT NULL,
        SimilarityPercentage     DECIMAL(5,2)    NOT NULL,
        Message                  NVARCHAR(300)       NULL
    );

    PRINT '>> dbo.DocumentComparisonFieldType created (Module 5C).';
END
ELSE
BEGIN
    PRINT '>> dbo.DocumentComparisonFieldType already exists - skipped.';
END
GO

/*------------------------------------------------------------------------------
  usp_DocumentComparison_Save   ->  POST /api/fraud/compare/{propertyId}
  Inserts one DocumentComparison header row plus its DocumentComparisonField
  rows in a single transaction, then returns both result sets (header, then
  fields) so the caller never needs a second round trip to read back what it
  just saved.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_DocumentComparison_Save
    @PropertyID             INT,
    @ComparedByUserID       INT,
    @DocumentReference      NVARCHAR(300)                       = NULL,
    @OverallMatchPercentage DECIMAL(5,2),
    @Summary                NVARCHAR(MAX)                       = NULL,
    @Fields                 dbo.DocumentComparisonFieldType READONLY,
    @NewComparisonID        INT                                  = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @ComparedByUserID)
    BEGIN
        RAISERROR (N'Comparing user not found.', 16, 1);
        RETURN -1;
    END

    DECLARE @FieldsCompared INT = (SELECT COUNT(*) FROM @Fields);
    DECLARE @FieldsMatched  INT = (SELECT COUNT(*) FROM @Fields WHERE Matched = 1);

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.DocumentComparison
            (PropertyID, ComparedByUserID, DocumentReference, FieldsCompared, FieldsMatched, OverallMatchPercentage, Summary)
        VALUES
            (@PropertyID, @ComparedByUserID, @DocumentReference, @FieldsCompared, @FieldsMatched, @OverallMatchPercentage, @Summary);

        SET @NewComparisonID = SCOPE_IDENTITY();

        INSERT INTO dbo.DocumentComparisonField
            (ComparisonID, FieldName, OcrValue, DatabaseValue, Matched, SimilarityPercentage, Message)
        SELECT
            @NewComparisonID, FieldName, OcrValue, DatabaseValue, Matched, SimilarityPercentage, Message
        FROM @Fields;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT
        ComparisonID, PropertyID, ComparedByUserID, DocumentReference,
        FieldsCompared, FieldsMatched, OverallMatchPercentage, Summary, ComparisonDate
    FROM dbo.DocumentComparison
    WHERE ComparisonID = @NewComparisonID;

    SELECT
        ComparisonFieldID, FieldName, OcrValue, DatabaseValue, Matched, SimilarityPercentage, Message
    FROM dbo.DocumentComparisonField
    WHERE ComparisonID = @NewComparisonID
    ORDER BY ComparisonFieldID;

    RETURN 0;
END;
GO

PRINT '>> usp_DocumentComparison_Save created (Module 5C).';
GO

/*------------------------------------------------------------------------------
  usp_DocumentComparison_GetLatest   ->  GET /api/fraud/comparison/{propertyId}
  Returns the most recent comparison run for a property (header, then its
  fields) - both result sets empty, not an error, if the property has never
  been compared yet, so the C# layer can treat "no rows" as a normal,
  expected outcome rather than a SqlException.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_DocumentComparison_GetLatest
    @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ComparisonID INT;

    SELECT TOP (1) @ComparisonID = ComparisonID
    FROM dbo.DocumentComparison
    WHERE PropertyID = @PropertyID
    ORDER BY ComparisonDate DESC, ComparisonID DESC;

    SELECT
        ComparisonID, PropertyID, ComparedByUserID, DocumentReference,
        FieldsCompared, FieldsMatched, OverallMatchPercentage, Summary, ComparisonDate
    FROM dbo.DocumentComparison
    WHERE ComparisonID = @ComparisonID;

    SELECT
        ComparisonFieldID, FieldName, OcrValue, DatabaseValue, Matched, SimilarityPercentage, Message
    FROM dbo.DocumentComparisonField
    WHERE ComparisonID = @ComparisonID
    ORDER BY ComparisonFieldID;

    RETURN 0;
END;
GO

PRINT '>> usp_DocumentComparison_GetLatest created (Module 5C).';
GO
