/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : 06_TestQueries.sql
  Purpose : Verification queries and worked API examples. Run this after the
            seed script to confirm the database behaves as documented.
  Author  : Ladhurshan Sivasathyamoorthy
==============================================================================*/

USE LandGuardDB;
GO

PRINT '==============================================================';
PRINT ' TEST 1 - Every table is populated';
PRINT '==============================================================';
GO

SELECT t.name AS TableName, SUM(p.rows) AS TotalRows
FROM sys.tables AS t
INNER JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY t.name
ORDER BY t.name;
GO


PRINT '==============================================================';
PRINT ' TEST 2 - Every property was scored, and the score matches the';
PRINT '          sum of the weights of the rules that fired';
PRINT '==============================================================';
GO

SELECT
    p.PropertyID,
    LEFT(p.Title, 40)                       AS Title,
    r.RiskScore,
    r.RiskLevel,
    r.FraudStatus,
    p.Status                                AS ListingStatus,
    (SELECT SUM(d.PointsAdded)
       FROM dbo.vw_FraudCheckDetail AS d
      WHERE d.PropertyID = p.PropertyID)    AS RecomputedScore,
    CASE WHEN r.RiskScore = (SELECT SUM(d.PointsAdded)
                               FROM dbo.vw_FraudCheckDetail AS d
                              WHERE d.PropertyID = p.PropertyID)
         THEN 'PASS' ELSE 'FAIL' END        AS ScoreCheck,
    CASE WHEN r.RiskLevel = dbo.fn_RiskLevelFromScore(r.RiskScore)
         THEN 'PASS' ELSE 'FAIL' END        AS BandCheck
FROM dbo.Property AS p
LEFT JOIN dbo.vw_PropertyLatestRisk AS r ON r.PropertyID = p.PropertyID
ORDER BY p.PropertyID;
GO


PRINT '==============================================================';
PRINT ' TEST 3 - Risk distribution across the dataset';
PRINT '==============================================================';
GO

SELECT RiskLevel, COUNT(*) AS Listings,
       MIN(RiskScore) AS MinScore, MAX(RiskScore) AS MaxScore
FROM dbo.vw_PropertyLatestRisk
GROUP BY RiskLevel
ORDER BY MIN(RiskScore);
GO


PRINT '==============================================================';
PRINT ' TEST 4 - How often each of the 7 rules fired';
PRINT '==============================================================';
GO

SELECT * FROM dbo.vw_RuleTriggerFrequency ORDER BY TimesTriggered DESC;
GO


PRINT '==============================================================';
PRINT ' TEST 5 - The worst listing, rule by rule (fraud report, FR06)';
PRINT '==============================================================';
GO

SELECT TOP (1) PropertyID, Title, RiskScore, RiskLevel
FROM dbo.vw_PropertyListing
ORDER BY RiskScore DESC;

SELECT RuleName, Triggered, PointsAdded, MaxPoints, Description
FROM dbo.vw_FraudCheckDetail
WHERE PropertyID = (SELECT TOP (1) PropertyID FROM dbo.vw_PropertyListing
                    ORDER BY RiskScore DESC)
ORDER BY PointsAdded DESC;
GO


PRINT '==============================================================';
PRINT ' TEST 6 - Planted duplicate deed references were all detected';
PRINT '==============================================================';
GO

SELECT p.DeedReference,
       COUNT(*)                                   AS ListingsSharingDeed,
       STRING_AGG(CAST(p.PropertyID AS VARCHAR(10)), ', ') AS PropertyIDs,
       MIN(CAST(f.DeedCheck AS INT))              AS AllFlagged  -- must be 1
FROM dbo.Property AS p
INNER JOIN dbo.vw_PropertyLatestRisk AS f ON f.PropertyID = p.PropertyID
WHERE p.DeedReference IS NOT NULL
GROUP BY p.DeedReference
HAVING COUNT(*) > 1
ORDER BY p.DeedReference;
GO


PRINT '==============================================================';
PRINT ' TEST 7 - Planted duplicate images were all detected';
PRINT '==============================================================';
GO

SELECT i.ImageHash,
       COUNT(DISTINCT i.PropertyID)                        AS PropertiesSharingImage,
       STRING_AGG(CAST(i.PropertyID AS VARCHAR(10)), ', ') AS PropertyIDs,
       MIN(CAST(f.DuplicateCheck AS INT))                  AS AllFlagged  -- must be 1
FROM dbo.PropertyImage AS i
INNER JOIN dbo.vw_PropertyLatestRisk AS f ON f.PropertyID = i.PropertyID
WHERE i.ImageHash IS NOT NULL
GROUP BY i.ImageHash
HAVING COUNT(DISTINCT i.PropertyID) > 1;
GO


PRINT '==============================================================';
PRINT ' TEST 8 - Only Low risk listings publish automatically';
PRINT '==============================================================';
GO

SELECT r.RiskLevel, p.Status, COUNT(*) AS Listings
FROM dbo.Property AS p
INNER JOIN dbo.vw_PropertyLatestRisk AS r ON r.PropertyID = p.PropertyID
GROUP BY r.RiskLevel, p.Status
ORDER BY r.RiskLevel, p.Status;
GO


PRINT '==============================================================';
PRINT ' TEST 9 - Referential integrity: no orphans anywhere';
PRINT '==============================================================';
GO

SELECT 'Property -> Users'          AS Relationship,
       COUNT(*) AS OrphanRows FROM dbo.Property p
       LEFT JOIN dbo.Users u ON u.UserID = p.SellerID WHERE u.UserID IS NULL
UNION ALL
SELECT 'PropertyImage -> Property',  COUNT(*) FROM dbo.PropertyImage i
       LEFT JOIN dbo.Property p ON p.PropertyID = i.PropertyID WHERE p.PropertyID IS NULL
UNION ALL
SELECT 'FraudCheck -> Property',     COUNT(*) FROM dbo.FraudCheck f
       LEFT JOIN dbo.Property p ON p.PropertyID = f.PropertyID WHERE p.PropertyID IS NULL
UNION ALL
SELECT 'RiskReport -> FraudCheck',   COUNT(*) FROM dbo.RiskReport r
       LEFT JOIN dbo.FraudCheck f ON f.FraudCheckID = r.FraudCheckID WHERE f.FraudCheckID IS NULL
UNION ALL
SELECT 'SuspiciousReport -> Users',  COUNT(*) FROM dbo.SuspiciousReport s
       LEFT JOIN dbo.Users u ON u.UserID = s.BuyerID WHERE u.UserID IS NULL
UNION ALL
SELECT 'Notification -> Users',      COUNT(*) FROM dbo.Notification n
       LEFT JOIN dbo.Users u ON u.UserID = n.UserID WHERE u.UserID IS NULL
UNION ALL
SELECT 'Podcast -> Users(Admin)',    COUNT(*) FROM dbo.Podcast pc
       LEFT JOIN dbo.Users u ON u.UserID = pc.AdminID AND u.Role = 'Admin' WHERE u.UserID IS NULL;
GO


PRINT '==============================================================';
PRINT ' TEST 10 - Every fraud check has exactly one risk report (1:1)';
PRINT '==============================================================';
GO

SELECT
    (SELECT COUNT(*) FROM dbo.FraudCheck) AS FraudChecks,
    (SELECT COUNT(*) FROM dbo.RiskReport) AS RiskReports,
    CASE WHEN (SELECT COUNT(*) FROM dbo.FraudCheck) = (SELECT COUNT(*) FROM dbo.RiskReport)
         THEN 'PASS' ELSE 'FAIL' END AS OneToOneCheck;
GO


/*==============================================================================
  WORKED API EXAMPLES
  Each block below is the SQL behind one endpoint in the API Development plan.
==============================================================================*/

PRINT '==============================================================';
PRINT ' API - GET /api/properties  (search, filter, page)';
PRINT '==============================================================';
GO

EXEC dbo.usp_Property_Search
     @Keyword    = NULL,
     @District   = N'Colombo',
     @MinPrice   = NULL,
     @MaxPrice   = NULL,
     @RiskLevel  = 'Low',
     @SortBy     = 'PriceAsc',
     @PageNumber = 1,
     @PageSize   = 10;
GO

PRINT '==============================================================';
PRINT ' API - GET /api/properties/{id}  (details + images + fraud report)';
PRINT '==============================================================';
GO

EXEC dbo.usp_Property_GetById @PropertyID = 21;
GO

PRINT '==============================================================';
PRINT ' API - GET /api/admin/flagged';
PRINT '==============================================================';
GO

EXEC dbo.usp_Admin_GetFlagged;
GO

PRINT '==============================================================';
PRINT ' API - admin dashboard (stats + rule frequency + review queue)';
PRINT '==============================================================';
GO

EXEC dbo.usp_Admin_GetDashboard;
GO

PRINT '==============================================================';
PRINT ' API - POST /api/properties  (full submit -> fraud analysis)';
PRINT '        A clean listing from a verified seller should score 0.';
PRINT '==============================================================';
GO

DECLARE @NewID INT;

EXEC dbo.usp_Property_Create
     @SellerID      = 3,
     @Title         = N'End to end test listing - Battaramulla',
     @Description   = N'Test listing submitted through the API to confirm the fraud engine executes on every submission as required by NFR04.',
     @Location      = N'Battaramulla, Colombo',
     @District      = N'Colombo',
     @Latitude      = 6.898900,
     @Longitude     = 79.918800,
     @Size          = 15,
     @Price         = 52500000.00,
     @DeedReference = 'COL/2026/DEED/TEST1',
     @NewPropertyID = @NewID OUTPUT;

-- Attach an image, then re-run the engine so the image rules see it
EXEC dbo.usp_PropertyImage_Add
     @PropertyID = @NewID,
     @ImageURL   = N'/uploads/properties/test_front.jpg',
     @ImageHash  = 'TEST_UNIQUE_HASH_0001',
     @IsPrimary  = 1;

EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @NewID;

SELECT PropertyID, Title, Status, RiskScore, RiskLevel, FraudStatus
FROM dbo.vw_PropertyListing
WHERE PropertyID = @NewID;
GO

PRINT '==============================================================';
PRINT ' API - POST /api/reports  (buyer reports a suspicious listing)';
PRINT '==============================================================';
GO

EXEC dbo.usp_SuspiciousReport_Create
     @BuyerID     = 13,
     @PropertyID  = 25,
     @Reason      = N'Location does not exist',
     @Description = N'The coordinates are missing and the described beach frontage cannot be found.';
GO

PRINT '==============================================================';
PRINT ' API - notifications raised by the engine and by admin actions';
PRINT '==============================================================';
GO

EXEC dbo.usp_Notification_GetByUser @UserID = 9, @UnreadOnly = 0;
GO

PRINT '==============================================================';
PRINT ' API - fraud awareness podcasts by language (FR11 / NFR06)';
PRINT '==============================================================';
GO

EXEC dbo.usp_Podcast_GetAll @Language = NULL;
GO

PRINT '==============================================================';
PRINT ' RULE TUNING - lower the price rule weight and re-score';
PRINT '   (Chapter 3.3 mitigation: adjust thresholds without a redeploy)';
PRINT '==============================================================';
GO

-- Uncomment to demonstrate live re-tuning of the engine:
-- EXEC dbo.usp_Admin_UpdateRuleWeight @RuleCode = 'PRICE_ANOMALY', @Weight = 10, @Reanalyse = 1;
-- SELECT PropertyID, RiskScore, RiskLevel FROM dbo.vw_PropertyLatestRisk ORDER BY PropertyID;

PRINT '>> All tests complete.';
GO
