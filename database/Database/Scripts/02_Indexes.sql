/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : 02_Indexes.sql
  Purpose : Non-clustered indexes supporting FR10 (search & filter) and the
            fraud detection lookups. Directly supports NFR01 (pages load in
            under 3 seconds).
  Author  : Ladhurshan Sivasathyamoorthy
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  USERS
------------------------------------------------------------------------------*/
-- NIC uniqueness (FR02). Filtered so that buyers who register without a NIC
-- are not blocked - a plain UNIQUE constraint would permit only one NULL.
CREATE UNIQUE NONCLUSTERED INDEX UX_Users_NIC
    ON dbo.Users (NIC) WHERE NIC IS NOT NULL;
GO

-- Login lookup (AuthController: POST /api/auth/login)
CREATE NONCLUSTERED INDEX IX_Users_Email_Role
    ON dbo.Users (Email) INCLUDE (Role, IsActive, PasswordHash);
GO

-- Admin user management screen
CREATE NONCLUSTERED INDEX IX_Users_Role_IsActive
    ON dbo.Users (Role, IsActive) INCLUDE (Name, Email, NICVerified);
GO


/*------------------------------------------------------------------------------
  PROPERTY  -  search and filter (FR10)
------------------------------------------------------------------------------*/
-- Seller dashboard: "my listings"
CREATE NONCLUSTERED INDEX IX_Property_Seller_Status
    ON dbo.Property (SellerID, Status) INCLUDE (Title, Price, UploadDate);
GO

-- Buyer search: published listings ordered newest first
CREATE NONCLUSTERED INDEX IX_Property_Status_UploadDate
    ON dbo.Property (Status, UploadDate DESC) INCLUDE (Title, Location, District, Price, Size);
GO

-- Filter by district then price band
CREATE NONCLUSTERED INDEX IX_Property_District_Price
    ON dbo.Property (District, Price) INCLUDE (Title, Size, Status, PricePerPerch);
GO

-- Fraud CHECK 4: deed reference duplicate detection.
-- Filtered so that NULL deeds (still allowed at draft stage) are excluded.
CREATE NONCLUSTERED INDEX IX_Property_DeedReference
    ON dbo.Property (DeedReference) INCLUDE (SellerID, Status)
    WHERE DeedReference IS NOT NULL;
GO

-- Fraud CHECK 1: benchmark comparison by district
CREATE NONCLUSTERED INDEX IX_Property_PricePerPerch
    ON dbo.Property (District, PricePerPerch) INCLUDE (Status, Price, Size);
GO


/*------------------------------------------------------------------------------
  PROPERTY_IMAGE  -  fraud CHECK 2 (duplicate image)
------------------------------------------------------------------------------*/
CREATE NONCLUSTERED INDEX IX_PropertyImage_Hash
    ON dbo.PropertyImage (ImageHash) INCLUDE (PropertyID)
    WHERE ImageHash IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX IX_PropertyImage_Property
    ON dbo.PropertyImage (PropertyID, IsPrimary DESC) INCLUDE (ImageURL);
GO


/*------------------------------------------------------------------------------
  FRAUD_CHECK / RISK_REPORT
------------------------------------------------------------------------------*/
-- Retrieve the latest analysis run for a property
CREATE NONCLUSTERED INDEX IX_FraudCheck_Property_Date
    ON dbo.FraudCheck (PropertyID, CheckDate DESC) INCLUDE (FraudStatus);
GO

-- Filter listings by risk level (FR10)
CREATE NONCLUSTERED INDEX IX_RiskReport_Level_Score
    ON dbo.RiskReport (RiskLevel, RiskScore DESC) INCLUDE (FraudCheckID, GeneratedDate);
GO


/*------------------------------------------------------------------------------
  SUSPICIOUS_REPORT / NOTIFICATION / SAVED_PROPERTY / ADMIN_ACTION
------------------------------------------------------------------------------*/
CREATE NONCLUSTERED INDEX IX_SuspiciousReport_Status
    ON dbo.SuspiciousReport (Status, ReportDate DESC) INCLUDE (PropertyID, BuyerID, Reason);
GO

CREATE NONCLUSTERED INDEX IX_SuspiciousReport_Property
    ON dbo.SuspiciousReport (PropertyID) INCLUDE (Status);
GO

-- Buyer/seller notification bell: unread first
CREATE NONCLUSTERED INDEX IX_Notification_User_Status
    ON dbo.Notification (UserID, Status, NotificationDate DESC) INCLUDE (Message);
GO

CREATE NONCLUSTERED INDEX IX_SavedProperty_Buyer
    ON dbo.SavedProperty (BuyerID, SavedDate DESC) INCLUDE (PropertyID);
GO

CREATE NONCLUSTERED INDEX IX_AdminAction_Property
    ON dbo.AdminAction (PropertyID, ActionDate DESC) INCLUDE (AdminID, ActionType);
GO

CREATE NONCLUSTERED INDEX IX_AdminAction_Admin_Date
    ON dbo.AdminAction (AdminID, ActionDate DESC) INCLUDE (ActionType);
GO

PRINT '>> 16 non-clustered indexes created.';
GO
