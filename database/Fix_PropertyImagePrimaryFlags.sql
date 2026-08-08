/*==============================================================================
  LANDGUARD - Fix: duplicate primary property images
  ------------------------------------------------------------------------------
  Script  : Fix_PropertyImagePrimaryFlags.sql
  Purpose : Safety-net fix for a live database whose dbo.PropertyImage table
            has drifted from database/Database/Scripts/05_SeedData.sql and
            ended up with more than one IsPrimary = 1 row for the same
            PropertyID (symptom: the property details page shows two
            "Primary" badges).

  INVESTIGATION NOTE (see the accompanying report)
      05_SeedData.sql itself was checked and contains exactly ONE
      IsPrimary = 1 row per property, for all 31 seeded properties - so
      this script is not "fixing the seed script" (nothing there is
      wrong). It exists purely to repair a live database instance if one
      has drifted from that script (e.g. a duplicate seed run, or a
      manual edit) and ended up with duplicate primaries. Running it
      against a database that has no duplicates is a safe no-op.

  WHAT THIS DOES NOT TOUCH
      - ImageHash values (the fraud engine's duplicate-image test
        fixtures - HASH_DUP_A..E - are read-only here)
      - ImageURL values
      - Any Property, FraudCheck, RiskReport or other fraud-related table
      - Any row where a PropertyID already has exactly one primary image

  RULE
      For every PropertyID with more than one IsPrimary = 1 row, keep the
      lowest ImageID (the first image ever added for that property) as
      primary and set every other image for that property to
      IsPrimary = 0. Idempotent - safe to run more than once.
==============================================================================*/

USE LandGuardDB;
GO

-- Optional: run this first to see exactly which properties (if any) are
-- affected before applying the fix below.
--
-- SELECT PropertyID, COUNT(*) AS PrimaryCount
-- FROM dbo.PropertyImage
-- WHERE IsPrimary = 1
-- GROUP BY PropertyID
-- HAVING COUNT(*) > 1;

;WITH RankedPrimaryImages AS (
    SELECT
        ImageID,
        PropertyID,
        ROW_NUMBER() OVER (PARTITION BY PropertyID ORDER BY ImageID ASC) AS PrimaryRank
    FROM dbo.PropertyImage
    WHERE IsPrimary = 1
)
UPDATE pi
    SET pi.IsPrimary = 0
FROM dbo.PropertyImage AS pi
INNER JOIN RankedPrimaryImages AS r
    ON r.ImageID = pi.ImageID
WHERE r.PrimaryRank > 1;

PRINT '>> PropertyImage primary-flag cleanup complete.';
GO

-- Verification - should return zero rows after the fix above.
SELECT PropertyID, COUNT(*) AS PrimaryCount
FROM dbo.PropertyImage
WHERE IsPrimary = 1
GROUP BY PropertyID
HAVING COUNT(*) > 1;
GO
