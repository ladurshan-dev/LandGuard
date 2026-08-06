/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : Module3_ChangePassword.sql
  Purpose : Adds usp_User_ChangePassword, the one stored procedure Module 3
            (JWT Authentication) needs that the Module 2 database package
            (Database/Scripts/01-06) did not include. Authentication
            supports Register (INSERT), Login (SELECT) and GetById (SELECT)
            for dbo.Users, but has no procedure that can update
            PasswordHash after registration - this fills that one gap.

  Why this is a separate file instead of editing 04_StoredProcedures.sql:
    This solution's copy of the repository does not contain the canonical
    Database/Scripts folder (it lives in the database owner's own
    checkout). Adding a new, additive script here means Module 2's
    existing files are never touched. Please fold this procedure into
    Database/Scripts/04_StoredProcedures.sql (Section B - Authentication,
    immediately after usp_User_GetById) the next time that repository is
    updated, so every environment builds from one canonical set of scripts.

  Nature of the change : ADDITIVE ONLY.
    - No ALTER TABLE, no new column, no new constraint, no schema change.
    - One new CREATE OR ALTER PROCEDURE, following the exact conventions
      of every other procedure in Section B/F (SET NOCOUNT ON, an
      existence/active check with RAISERROR + RETURN, a TRY/CATCH
      transaction around the write, a Notification insert as a side
      effect, a final SELECT result set, RETURN 0 on success) - modelled
      directly on usp_Admin_SetUserActive.
  Author  : LandGuard Module 3 (Authentication)
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  usp_User_ChangePassword   ->  POST /api/auth/change-password
  The new password is hashed in the API layer (BCrypt) before this is
  called, exactly like usp_User_Register - only the hash ever reaches SQL.
  Requires the caller to already be authenticated (the API verifies the
  current password against the existing hash before calling this
  procedure); this procedure itself only re-checks that the account still
  exists and is active, then performs the update.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_User_ChangePassword
    @UserID          INT,
    @NewPasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserID = @UserID AND IsActive = 1)
    BEGIN
        RAISERROR (N'User not found, or the account is suspended.', 16, 1);
        RETURN -1;
    END

    DECLARE @RowsUpdated INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Users
        SET PasswordHash = @NewPasswordHash
        WHERE UserID = @UserID;

        SET @RowsUpdated = @@ROWCOUNT;

        INSERT INTO dbo.Notification (UserID, Message)
        VALUES (@UserID,
                N'Your password was changed. If this was not you, contact an administrator immediately.');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @RowsUpdated AS RowsUpdated;
    RETURN 0;
END;
GO

PRINT '>> usp_User_ChangePassword created (Module 3).';
GO
