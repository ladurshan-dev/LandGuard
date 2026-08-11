/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : 03_Views.sql
  Purpose : Read models consumed by the ASP.NET Core Web API. Each view maps to
            a screen in the frontend so the API layer stays thin.
  Author  : Ladhurshan Sivasathyamoorthy
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  vw_PropertyLatestRisk
  Helper view: the most recent fraud check + risk report for every property.
  Everything else builds on top of this.
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_PropertyLatestRisk
AS
SELECT
    fc.PropertyID,
    fc.FraudCheckID,
    fc.PriceCheck,
    fc.DuplicateCheck,
    fc.NICCheck,
    fc.DeedCheck,
    fc.SellerHistoryCheck,
    fc.LocationCheck,
    fc.MissingInfoCheck,
    fc.FraudStatus,
    fc.CheckDate,
    rr.ReportID,
    rr.RiskScore,
    rr.RiskLevel,
    rr.Summary,
    rr.GeneratedDate
FROM dbo.FraudCheck AS fc
LEFT JOIN dbo.RiskReport AS rr
       ON rr.FraudCheckID = fc.FraudCheckID
WHERE fc.FraudCheckID = (
        SELECT MAX(fc2.FraudCheckID)
        FROM dbo.FraudCheck AS fc2
        WHERE fc2.PropertyID = fc.PropertyID
      );
GO


/*------------------------------------------------------------------------------
  vw_PropertyListing
  Main buyer-facing listing feed: property + seller + risk badge + cover image.
  Used by GET /api/properties and GET /api/properties/{id}.
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


/*------------------------------------------------------------------------------
  vw_PublishedProperty
  Only what a buyer is allowed to see: approved listings from active sellers.
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_PublishedProperty
AS
SELECT v.*
FROM dbo.vw_PropertyListing AS v
INNER JOIN dbo.Users AS u ON u.UserID = v.SellerID
WHERE v.Status = 'Approved'
  AND u.IsActive = 1;
GO


/*------------------------------------------------------------------------------
  vw_FraudCheckDetail
  Row-per-rule breakdown of the 8-point engine, used to render the fraud report
  shown to buyers (FR06) and the reason list shown to sellers.
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_FraudCheckDetail
AS
SELECT  f.PropertyID,
        f.FraudCheckID,
        x.RuleCode,
        w.RuleName,
        x.Triggered,
        CASE WHEN x.Triggered = 1 THEN w.Weight ELSE 0 END AS PointsAdded,
        w.Weight AS MaxPoints,
        w.Description
FROM dbo.vw_PropertyLatestRisk AS f
CROSS APPLY (VALUES
        ('PRICE_ANOMALY',    f.PriceCheck),
        ('IMAGE_DUPLICATE',  f.DuplicateCheck),
        ('NIC_VERIFICATION', f.NICCheck),
        ('DEED_DUPLICATE',   f.DeedCheck),
        ('SELLER_HISTORY',   f.SellerHistoryCheck),
        ('LOCATION_INVALID', f.LocationCheck),
        ('MISSING_INFO',     f.MissingInfoCheck)
     ) AS x(RuleCode, Triggered)
INNER JOIN dbo.FraudRuleWeight AS w ON w.RuleCode = x.RuleCode;
GO


/*------------------------------------------------------------------------------
  vw_FlaggedProperty
  Admin review queue - GET /api/admin/flagged
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_FlaggedProperty
AS
SELECT
    v.PropertyID, v.Title, v.Location, v.District, v.Price, v.Size,
    v.DeedReference, v.Status, v.UploadDate,
    v.SellerID, v.SellerName, v.SellerNICVerified,
    v.RiskScore, v.RiskLevel, v.FraudStatus, v.RiskSummary,
    v.ReportCount,
    (SELECT COUNT(*) FROM dbo.SuspiciousReport AS sr
      WHERE sr.PropertyID = v.PropertyID AND sr.Status <> 'Resolved') AS OpenReportCount,
    DATEDIFF(DAY, v.UploadDate, SYSDATETIME()) AS DaysWaiting
FROM dbo.vw_PropertyListing AS v
WHERE v.Status <> 'Withdrawn'  -- Phase F: a withdrawn listing must never appear in the active Admin review queue, even via the open-report branch below
  AND (v.Status IN ('Flagged','Pending')
       OR EXISTS (SELECT 1 FROM dbo.SuspiciousReport AS sr
                   WHERE sr.PropertyID = v.PropertyID AND sr.Status <> 'Resolved'));
GO


/*------------------------------------------------------------------------------
  vw_SellerDashboard   (FR08)
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_SellerDashboard
AS
SELECT
    u.UserID        AS SellerID,
    u.Name          AS SellerName,
    u.NICVerified,
    u.IsActive,
    COUNT(p.PropertyID)                                                     AS TotalListings,
    SUM(CASE WHEN p.Status = 'Approved' THEN 1 ELSE 0 END)                  AS ApprovedListings,
    SUM(CASE WHEN p.Status = 'Pending'  THEN 1 ELSE 0 END)                  AS PendingListings,
    SUM(CASE WHEN p.Status = 'Flagged'  THEN 1 ELSE 0 END)                  AS FlaggedListings,
    SUM(CASE WHEN p.Status = 'Rejected' THEN 1 ELSE 0 END)                  AS RejectedListings,
    AVG(CAST(r.RiskScore AS DECIMAL(5,2)))                                  AS AverageRiskScore
FROM dbo.Users AS u
LEFT JOIN dbo.Property AS p               ON p.SellerID   = u.UserID
LEFT JOIN dbo.vw_PropertyLatestRisk AS r  ON r.PropertyID = p.PropertyID
WHERE u.Role = 'Seller'
GROUP BY u.UserID, u.Name, u.NICVerified, u.IsActive;
GO


/*------------------------------------------------------------------------------
  vw_BuyerSavedProperty   (FR07)
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_BuyerSavedProperty
AS
SELECT
    s.SavedPropertyID, s.BuyerID, s.SavedDate,
    v.PropertyID, v.Title, v.Location, v.District, v.Price, v.Size,
    v.Status, v.RiskScore, v.RiskLevel, v.CoverImageURL, v.SellerName
FROM dbo.SavedProperty AS s
INNER JOIN dbo.vw_PropertyListing AS v ON v.PropertyID = s.PropertyID;
GO


/*------------------------------------------------------------------------------
  vw_FraudStatistics
  Single-row summary for the admin dashboard.
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_FraudStatistics
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Users    WHERE Role = 'Buyer')                    AS TotalBuyers,
    (SELECT COUNT(*) FROM dbo.Users    WHERE Role = 'Seller')                   AS TotalSellers,
    (SELECT COUNT(*) FROM dbo.Users    WHERE Role = 'Seller' AND NICVerified=1) AS VerifiedSellers,
    (SELECT COUNT(*) FROM dbo.Users    WHERE IsActive = 0)                      AS SuspendedUsers,
    (SELECT COUNT(*) FROM dbo.Property)                                         AS TotalProperties,
    (SELECT COUNT(*) FROM dbo.Property WHERE Status = 'Approved')               AS ApprovedProperties,
    (SELECT COUNT(*) FROM dbo.Property WHERE Status = 'Pending')                AS PendingProperties,
    (SELECT COUNT(*) FROM dbo.Property WHERE Status = 'Flagged')                AS FlaggedProperties,
    (SELECT COUNT(*) FROM dbo.Property WHERE Status = 'Rejected')               AS RejectedProperties,
    (SELECT COUNT(*) FROM dbo.vw_PropertyLatestRisk WHERE RiskLevel = 'Low')    AS LowRiskCount,
    (SELECT COUNT(*) FROM dbo.vw_PropertyLatestRisk WHERE RiskLevel = 'Medium') AS MediumRiskCount,
    (SELECT COUNT(*) FROM dbo.vw_PropertyLatestRisk WHERE RiskLevel = 'High')   AS HighRiskCount,
    (SELECT AVG(CAST(RiskScore AS DECIMAL(5,2))) FROM dbo.vw_PropertyLatestRisk) AS AverageRiskScore,
    (SELECT COUNT(*) FROM dbo.SuspiciousReport WHERE Status = 'Open')           AS OpenSuspiciousReports,
    (SELECT COUNT(*) FROM dbo.Podcast)                                          AS TotalPodcasts;
GO


/*------------------------------------------------------------------------------
  vw_RuleTriggerFrequency
  How often each rule fires - evidence for tuning the thresholds listed in the
  Chapter 3.3 Risk Analysis.
------------------------------------------------------------------------------*/
CREATE OR ALTER VIEW dbo.vw_RuleTriggerFrequency
AS
SELECT
    w.RuleCode,
    w.RuleName,
    w.Weight,
    SUM(CASE WHEN d.Triggered = 1 THEN 1 ELSE 0 END)  AS TimesTriggered,
    COUNT(d.PropertyID)                               AS TimesEvaluated,
    CAST(100.0 * SUM(CASE WHEN d.Triggered = 1 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(d.PropertyID),0) AS DECIMAL(5,2)) AS TriggerRatePercent
FROM dbo.FraudRuleWeight AS w
LEFT JOIN dbo.vw_FraudCheckDetail AS d ON d.RuleCode = w.RuleCode
GROUP BY w.RuleCode, w.RuleName, w.Weight;
GO

PRINT '>> 9 views created.';
GO
