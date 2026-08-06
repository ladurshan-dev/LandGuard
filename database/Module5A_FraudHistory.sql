/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : Module5A_FraudHistory.sql
  Purpose : Adds usp_Fraud_GetHistory, the one stored procedure Module 5A
            (Fraud Detection Foundation) needs that the Module 2 database
            package (Database/Scripts/01-06) did not include.

  Context: the existing fraud engine (usp_Fraud_AnalyseProperty,
  usp_Risk_GenerateReport, dbo.FraudCheck, dbo.RiskReport,
  dbo.FraudRuleWeight) is reused as-is by Module 5A - nothing about it is
  changed, replaced, or duplicated. dbo.FraudCheck already keeps one row
  per analysis run ("A property may be analysed more than once ... this
  table keeps full history"), but no existing view or procedure exposes
  that history - vw_PropertyLatestRisk deliberately surfaces only the
  latest run, and usp_Property_GetById's fraud result set is likewise
  latest-only (it reads from vw_FraudCheckDetail, which is built on
  vw_PropertyLatestRisk). This is the one genuine gap Module 5A needs
  filled: "how many times has this listing been analysed, and what did
  each run score."

  Why this is a separate file instead of editing 04_StoredProcedures.sql:
    Same reason as Module 3's Module3_ChangePassword.sql - this checkout
    does not contain the canonical Database/Scripts folder (it lives in
    the database owner's own checkout). Adding a new, additive script here
    means Module 2's existing files are never touched. Please fold this
    procedure into Database/Scripts/04_StoredProcedures.sql (Section D -
    Fraud Detection Engine, immediately after usp_Fraud_ReanalyseAll) the
    next time that repository is updated.

  Nature of the change : ADDITIVE, READ-ONLY ONLY.
    - No ALTER TABLE, no new column, no new constraint, no schema change.
    - No change to usp_Fraud_AnalyseProperty, usp_Risk_GenerateReport, or
      any existing view.
    - One new CREATE OR ALTER PROCEDURE, SELECT-only, following the same
      conventions as the rest of Section D (SET NOCOUNT ON, an existence
      check with RAISERROR + RETURN).
  Author  : LandGuard Module 5A (Fraud Detection Foundation)
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  usp_Fraud_GetHistory   ->  GET /api/fraud/history/{propertyId}
  Every analysis run for a property (FraudCheck joined 1:1 to its
  RiskReport, LEFT JOINed since a RiskReport can momentarily not exist yet
  between usp_Fraud_AnalyseProperty's insert and usp_Risk_GenerateReport's
  own insert), newest first.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Fraud_GetHistory
    @PropertyID INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Property WHERE PropertyID = @PropertyID)
    BEGIN
        RAISERROR (N'Property not found.', 16, 1);
        RETURN -1;
    END

    SELECT
        fc.FraudCheckID,
        fc.CheckDate,
        fc.FraudStatus,
        fc.PriceCheck,
        fc.DuplicateCheck,
        fc.NICCheck,
        fc.DeedCheck,
        fc.SellerHistoryCheck,
        fc.LocationCheck,
        fc.MissingInfoCheck,
        rr.ReportID,
        rr.RiskScore,
        rr.RiskLevel,
        rr.Summary,
        rr.GeneratedDate
    FROM dbo.FraudCheck AS fc
    LEFT JOIN dbo.RiskReport AS rr ON rr.FraudCheckID = fc.FraudCheckID
    WHERE fc.PropertyID = @PropertyID
    ORDER BY fc.CheckDate DESC, fc.FraudCheckID DESC;

    RETURN 0;
END;
GO

PRINT '>> usp_Fraud_GetHistory created (Module 5A).';
GO
