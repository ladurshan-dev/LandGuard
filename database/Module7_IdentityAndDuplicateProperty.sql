/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : Module7_IdentityAndDuplicateProperty.sql
  Purpose : Two independent, additive requirements delivered together:

  (B) SELLER GOVERNMENT IDENTITY VERIFICATION
      - dbo.Users.IdentityStatus (Pending | Verified | Failed) - the
        smallest backward-compatible addition needed because NICVerified
        (a BIT) cannot represent three states.
      - CORRECTION (post-review): NICVerified is NOT left untouched.
        Grepping the full backend/SQL/frontend surface found it is still a
        LIVE signal - it feeds CHECK 3 (SELLER NIC VERIFICATION) of the
        legacy usp_Fraud_AnalyseProperty supporting-risk engine, and it is
        projected through vw_PropertyListing/vw_PublishedProperties/
        vw_FlaggedProperty as SellerNICVerified, which
        PropertyListingResult.SellerNicVerified /
        PropertySearchResult.SellerNicVerified surface to the frontend as
        the "(NIC verified)" badge shown to Buyers and Admins
        (AdminPropertyDetailsPage/AdminPropertyReviewPage/
        BuyerPropertyDetailsPage). Leaving it unsynchronized would let a
        Seller who is IdentityStatus = Verified still show as NIC-unverified
        (or the reverse), and would let CHECK 3 misfire against a Seller
        who has actually passed automated identity verification. Both
        authoritative write paths are now kept in lockstep:
          * usp_User_SetIdentityStatus (below) - the automated path - now
            also writes NICVerified (Verified -> 1, Pending/Failed -> 0) in
            the same UPDATE.
          * usp_Admin_VerifyNIC (re-issued below) - the pre-existing manual
            Admin path (not yet wired to a REST endpoint, per
            AdminDashboard.tsx's own comment, but still a live,
            independently-callable authoritative write) - now also sets
            IdentityStatus = 'Verified' for a Seller row in the same
            procedure, so a manual Admin verification actually unlocks
            property listing (which gates on IdentityStatus, not
            NICVerified) instead of silently doing nothing.
      - usp_User_GetById re-issued to also return IdentityStatus.
      - New usp_User_SetIdentityStatus - the one place IdentityStatus is
        ever written, called by SellerIdentityVerificationService
        (Application layer) right after registration and from the
        Seller-authenticated reverify endpoint. Never called for a Buyer.

  (D) GLOBAL DUPLICATE-PROPERTY PREVENTION
      - dbo.Property.GovernmentPropertyReference NVARCHAR(50) NULL - the
        authoritative Government Registry parcel reference
        (GovernmentLandRecordDto.PropertyReference) this PropertyID last
        resolved to, so a second PropertyID resolving to the SAME
        government parcel can be detected. Nothing else already
        represents this - GovernmentLandRecordDto.PropertyReference only
        ever existed as OCR/comparison evidence
        (DeedVerificationField.GovernmentValue), never as a queryable,
        current, per-Property value.
      - usp_Property_Create re-issued: adds a concurrency-safe duplicate
        DeedReference check (sp_getapplock-serialized, see that
        procedure's own header comment for exactly how two simultaneous
        requests for the same deed are prevented from both succeeding) and
        an IdentityStatus = 'Verified' guard, both defense-in-depth
        alongside the Application-layer checks
        (PropertyService.CreateAsync / SellerIdentityVerificationService).
      - usp_Property_ApplyDeedVerificationOutcome re-issued: adds the
        'DuplicateProperty' -> Disapproved mapping and an optional
        @GovernmentPropertyReference parameter that persists the resolved
        parcel reference onto Property at the same time the status is
        updated.
      - CORRECTION (post-review, second pass): the ORIGINAL version of
        this re-issue trusted the caller's @GovernmentPropertyReference/
        @VerificationStatus and wrote them unconditionally - but the
        caller's own duplicate check
        (usp_Property_FindByGovernmentPropertyReference, called from
        GovernmentDeedComparisonService.CompareAsync) runs as a SEPARATE
        database round trip well before this procedure is ever reached
        (OCR + field comparison happen in between). That gap is a
        check-then-act race: two different PropertyIDs resolving to the
        SAME GovernmentPropertyReference could both pass the caller-side
        pre-check before either one writes. usp_Property_ApplyDeedVerificationOutcome
        is now the AUTHORITATIVE, concurrency-safe check-and-write point
        instead - under an sp_getapplock keyed on the normalized reference
        (same pattern as usp_Property_Create's DeedReference guard, a
        different lock-resource namespace so the two never collide), it
        re-checks for another PropertyID already holding the reference
        immediately before persisting, inside the same transaction as the
        write. Whichever concurrent call acquires the lock first wins and
        becomes the authoritative holder; the loser is downgraded to
        'DuplicateProperty' -> Disapproved inside this procedure itself,
        regardless of what verdict it originally arrived with, and never
        persists the reference. See that procedure's own header comment
        ("CONCURRENCY FIX") for the full explanation. The caller-side
        pre-check is NOT removed - it still gives an early, targeted
        duplicate report in the common non-racing case - but it is no
        longer the authority; this procedure's own lock-protected re-check
        is.
      - New usp_Property_FindByGovernmentPropertyReference - a narrow,
        privacy-safe read (PropertyID only, no Seller name/NIC/email) used
        by GovernmentDeedComparisonService for that early, non-authoritative
        pre-check.
      - CK_DeedVerification_Status / CK_DeedVerificationReason_Reason
        extended with 'DuplicateProperty' / 'DuplicatePropertyReference'
        (LandGuard.Domain.Enums.DeedVerificationStatus/DeedFraudReason's
        new members - inspected directly before writing this script).

  IMPORTANT - existing duplicate DeedReference data found and NOT touched:
  05_SeedData.sql's own header comment documents 7 DELIBERATELY PLANTED
  duplicate-DeedReference pairs among the 31 seed Property rows - (10,11)
  (12,13) (15,16) (21,22) (25,26) (28,29) (30,31) - built to exercise the
  LEGACY numeric fraud engine's own duplicate-deed rule. This means a
  database-level UNIQUE constraint/index on (normalized) DeedReference is
  UNSAFE to add: CREATE UNIQUE INDEX would fail outright the moment it
  scanned this existing data, and 05_SeedData.sql's raw INSERT statements
  (SET IDENTITY_INSERT ON, bypassing usp_Property_Create entirely) would
  no longer be re-runnable. Per this task's own explicit instruction to
  stop before an unsafe destructive/blocking change and report it instead
  of deleting/rewriting seed data, NO unique constraint is added here.
  Global duplicate prevention for every NEW listing going forward is
  instead enforced procedurally inside usp_Property_Create (the
  sp_getapplock-serialized check below), which does not require the
  column to be unique and does not touch any existing row.

  Nature of the change : ADDITIVE ONLY.
    - Two new nullable columns (Users.IdentityStatus, Property.
      GovernmentPropertyReference). No backfill that invents data -
      IdentityStatus is backfilled only from each Seller's own existing
      NICVerified value (see the backfill block below); GovernmentPropertyReference
      is left NULL for every existing row (no historical verification run
      to derive it from).
    - Two existing CHECK constraints extended (guarded, idempotent).
    - usp_Property_Create, usp_Property_ApplyDeedVerificationOutcome and
      usp_User_GetById re-issued (CREATE OR ALTER) - see each block below
      for exactly what changed and what did not.
    - Two new procedures (usp_User_SetIdentityStatus,
      usp_Property_FindByGovernmentPropertyReference). No DROP of
      anything.
  Author  : LandGuard Seller Identity Verification / Global Duplicate-Property Prevention requirement
==============================================================================*/

USE LandGuardDB;
GO

/*------------------------------------------------------------------------------
  1) dbo.Users.IdentityStatus
------------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.Users', 'IdentityStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD IdentityStatus VARCHAR(20) NULL;
    PRINT '>> dbo.Users.IdentityStatus added (Module 7).';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_IdentityStatus')
BEGIN
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_IdentityStatus
        CHECK (IdentityStatus IS NULL OR IdentityStatus IN ('Pending','Verified','Failed'));

    PRINT '>> CK_Users_IdentityStatus added (Module 7).';
END
GO

-- Backfill EXISTING Seller rows only, and only once (WHERE IdentityStatus
-- IS NULL makes this a no-op on every re-run after the first). Derived
-- from each Seller's own existing NICVerified value - NICVerified = 1
-- already represented a completed manual identity check under the old
-- model, so it maps to 'Verified' (preserves current effective ability to
-- list a property); NICVerified = 0 maps to 'Pending' (never 'Failed' -
-- there is no actual name/NIC mismatch evidence for these rows, and
-- fabricating a 'Failed' verdict with no evidence would violate the
-- "technical failure must not accuse the seller" principle applied to
-- historical data too). Buyers/Admins are left NULL - IdentityStatus does
-- not apply to them (Buyer registration performs no identity check; Admin
-- accounts are seed/admin-managed only).
UPDATE dbo.Users
SET IdentityStatus = CASE WHEN NICVerified = 1 THEN 'Verified' ELSE 'Pending' END
WHERE Role = 'Seller' AND IdentityStatus IS NULL;
GO

PRINT '>> dbo.Users.IdentityStatus backfilled for existing Sellers from NICVerified (Module 7).';
GO

/*------------------------------------------------------------------------------
  2) dbo.Property.GovernmentPropertyReference
------------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.Property', 'GovernmentPropertyReference') IS NULL
BEGIN
    ALTER TABLE dbo.Property ADD GovernmentPropertyReference NVARCHAR(50) NULL;
    PRINT '>> dbo.Property.GovernmentPropertyReference added (Module 7).';
END
GO

/*------------------------------------------------------------------------------
  3) CK_DeedVerification_Status - add 'DuplicateProperty'
     (LandGuard.Domain.Enums.DeedVerificationStatus's 7th member).
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerification_Status'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%DuplicateProperty%'
)
BEGIN
    ALTER TABLE dbo.DeedVerification DROP CONSTRAINT CK_DeedVerification_Status;
    ALTER TABLE dbo.DeedVerification ADD CONSTRAINT CK_DeedVerification_Status
        CHECK (VerificationStatus IN
            (N'Verified', N'Fraudulent', N'PriceAnomaly', N'Unverified', N'UnverifiedCancelled', N'FormMismatch', N'DuplicateProperty'));

    PRINT '>> CK_DeedVerification_Status upgraded to include DuplicateProperty (Module 7).';
END
GO

/*------------------------------------------------------------------------------
  4) CK_DeedVerificationReason_Reason - add 'DuplicatePropertyReference'
     (LandGuard.Domain.Enums.DeedFraudReason's 22nd member).
------------------------------------------------------------------------------*/
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DeedVerificationReason_Reason'
      AND OBJECT_DEFINITION(OBJECT_ID) NOT LIKE '%DuplicatePropertyReference%'
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
             N'FormOwnerNicMismatch', N'FormOwnerAddressMismatch', N'DuplicatePropertyReference'));

    PRINT '>> CK_DeedVerificationReason_Reason upgraded to include DuplicatePropertyReference (Module 7).';
END
GO

/*------------------------------------------------------------------------------
  usp_User_GetById - re-issued to also return IdentityStatus. Every other
  column and the WHERE clause are unchanged.
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

PRINT '>> usp_User_GetById upgraded to include IdentityStatus (Module 7).';
GO

/*------------------------------------------------------------------------------
  usp_User_SetIdentityStatus
  The only procedure that writes dbo.Users.IdentityStatus. Called by
  Application layer's SellerIdentityVerificationService, both right after
  a Seller registers and from the Seller-authenticated reverify endpoint.
  RAISERRORs for a non-Seller (Buyer/Admin identity status is never set) -
  the same defense-in-depth style usp_Property_Create's Owner-field guard
  already uses, backing up the Application-layer check that never calls
  this for anything but a Seller in the first place.
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

    -- NICVerified CONSISTENCY (post-review fix): kept in lockstep with
    -- IdentityStatus in the same UPDATE, so the two can never contradict
    -- each other. NICVerified is still a live signal - CHECK 3 of the
    -- legacy usp_Fraud_AnalyseProperty supporting-risk engine reads it, and
    -- it is projected to the frontend as the Buyer/Admin-facing
    -- "(NIC verified)" badge via vw_PropertyListing/vw_PublishedProperties/
    -- vw_FlaggedProperty -> SellerNICVerified -> PropertyListingResult.
    -- SellerNicVerified / PropertySearchResult.SellerNicVerified.
    -- Mapping: Verified -> 1, Pending -> 0, Failed -> 0.
    UPDATE dbo.Users
    SET IdentityStatus = @IdentityStatus,
        NICVerified = CASE WHEN @IdentityStatus = 'Verified' THEN 1 ELSE 0 END
    WHERE UserID = @UserID;

    SELECT UserID, Name, Email, Role, NIC, Phone, NICVerified, IsActive, IdentityStatus, CreatedAt
    FROM dbo.Users WHERE UserID = @UserID;

    RETURN 0;
END;
GO

PRINT '>> usp_User_SetIdentityStatus created, keeps NICVerified in lockstep (Module 7).';
GO

/*------------------------------------------------------------------------------
  usp_Admin_VerifyNIC - re-issued (originally defined pre-Module-7, no
  Identity-Status awareness). This is the OTHER authoritative "seller is
  verified" write path - a manual Admin action (not yet wired to a REST
  endpoint, per AdminDashboard.tsx's own comment, but still directly
  EXEC-able and part of the existing AdminActionType.VerifyNIC = 6
  vocabulary). Without this re-issue, an Admin manually verifying a seller
  would set NICVerified = 1 while leaving IdentityStatus untouched (still
  Pending/Failed/NULL) - the exact contradictory state flagged in review,
  and one that would silently fail to unlock property listing, since
  usp_Property_Create/PropertyService.CreateAsync gate on IdentityStatus,
  not NICVerified. NICVerified's own UPDATE is unconditional, unchanged
  from the original (any @TargetUserID); IdentityStatus is additionally set
  to 'Verified' only when the target is a Seller row, matching
  usp_User_SetIdentityStatus's own Seller-only rule for that column.
------------------------------------------------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Admin_VerifyNIC
    @AdminID      INT,
    @TargetUserID INT,
    @Remarks      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users SET NICVerified = 1 WHERE UserID = @TargetUserID;

    UPDATE dbo.Users SET IdentityStatus = 'Verified'
    WHERE UserID = @TargetUserID AND Role = 'Seller';

    INSERT INTO dbo.AdminAction (AdminID, ActionType, TargetUserID, Remarks)
    VALUES (@AdminID, 'VerifyNIC', @TargetUserID, @Remarks);

    INSERT INTO dbo.Notification (UserID, Message)
    VALUES (@TargetUserID, N'Your NIC has been verified. You can now list properties as a verified seller.');
END;
GO

PRINT '>> usp_Admin_VerifyNIC upgraded to also set IdentityStatus = Verified for a Seller (Module 7).';
GO

/*------------------------------------------------------------------------------
  usp_Property_Create - re-issued. Same signature/columns as Module 6's
  version (SellerID, Title, ..., OwnerName/OwnerNIC/OwnerAddress) plus two
  new guards ahead of the INSERT:

  1) IdentityStatus = 'Verified' - defense-in-depth backing up
     PropertyService.CreateAsync's own check (which already runs first,
     with the exact Seller-facing message this requirement specifies -
     see that method's own comment). Folded into the existing "seller
     found, active" check below rather than a second RAISERROR block, so
     a suspended account and an unverified account read as the same kind
     of "you may not do this yet" condition.

  2) Global duplicate DeedReference - concurrency-safe via
     sp_getapplock(@LockMode = 'Exclusive', @LockOwner = 'Transaction'),
     keyed on the normalized (UPPER + trimmed) deed value. Two concurrent
     Create calls for the SAME deed serialize on this lock: the first to
     acquire it proceeds to its own EXISTS check (which will not yet see
     the other's row, since neither has committed) and INSERTs; the
     second BLOCKS until the first's transaction COMMITs or ROLLBACKs,
     then acquires the lock itself and its own EXISTS check now DOES see
     the first's already-committed row, so it RAISERRORs instead of
     inserting a duplicate. @LockOwner = 'Transaction' releases the lock
     automatically at COMMIT/ROLLBACK (including on an unexpected
     disconnect mid-transaction), so no explicit sp_releaseapplock is
     needed. This check is GLOBAL (no SellerID filter) and looks at every
     Status (no Status filter) - a Withdrawn, Disapproved or Rejected
     property still blocks reuse of its deed, exactly as required. A
     schema-level UNIQUE index was considered and rejected - see this
     script's own header comment for the existing planted-duplicate seed
     data that makes one unsafe.
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

    IF NOT EXISTS (SELECT 1 FROM dbo.Users
                    WHERE UserID = @SellerID AND IdentityStatus = 'Verified')
    BEGIN
        RAISERROR (N'Your identity must be verified before you can list a property.', 16, 1);
        RETURN -7;
    END

    IF LTRIM(RTRIM(ISNULL(@OwnerName, N'')))     = N''
    OR LTRIM(RTRIM(ISNULL(@OwnerNIC, N'')))      = N''
    OR LTRIM(RTRIM(ISNULL(@OwnerAddress, N''))) = N''
    OR LTRIM(RTRIM(ISNULL(@DeedReference, N''))) = N''
    BEGIN
        RAISERROR (N'Owner Name, Owner NIC, Owner Address and Deed Number are all required to list a property.', 16, 1);
        RETURN -2;
    END

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

    EXEC dbo.usp_Fraud_AnalyseProperty @PropertyID = @NewPropertyID;

    SELECT * FROM dbo.vw_PropertyListing WHERE PropertyID = @NewPropertyID;
    RETURN 0;
END;
GO

PRINT '>> usp_Property_Create upgraded with IdentityStatus guard + concurrency-safe global duplicate-deed check (Module 7).';
GO

/*------------------------------------------------------------------------------
  usp_Property_FindByGovernmentPropertyReference
  Read-only, privacy-safe lookup: PropertyID only - never SellerID, Seller
  name/NIC/email, or any other private data, satisfying the "never reveal
  the other Seller's private details" requirement structurally (the
  caller cannot leak what this procedure never returns). Excludes the
  property currently being verified via @ExcludePropertyID so a property
  re-verifying against its own already-persisted reference is never
  flagged as a duplicate of itself.
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

PRINT '>> usp_Property_FindByGovernmentPropertyReference created (Module 7).';
GO

/*------------------------------------------------------------------------------
  usp_Property_ApplyDeedVerificationOutcome - re-issued.
  Adds:
    - @GovernmentPropertyReference (optional) - when the verdict is one
      that would persist a reference (Verified/PriceAnomaly), this
      procedure is now the AUTHORITATIVE, concurrency-safe check-and-write
      point for it - see the "CONCURRENCY FIX" block below.
    - 'DuplicateProperty' -> Disapproved, with the exact Seller-facing
      message this requirement specifies verbatim, and deliberately no
      other Seller's PropertyID/name/NIC/email anywhere in that message.
      This branch is now reached two ways: (a) the caller already decided
      it (GovernmentDeedComparisonService's own pre-check), or (b) THIS
      procedure decides it itself, having found a race the caller's
      pre-check could not see - see below.
    - @EffectiveVerificationStatus OUTPUT (post-review, third pass) - the
      VerificationStatus this procedure actually ended up applying, which
      only ever differs from the input @VerificationStatus in the race-
      downgrade case. GovernmentDeedVerificationService now calls this
      procedure BEFORE persisting the DeedVerification audit row, and uses
      this output to correct the audit record if a downgrade happened -
      see AUDIT-CONSISTENCY FIX below and GovernmentDeedVerificationService's
      own matching comment for the full explanation.
  Every other VerificationStatus branch, the Withdrawn guard, and the
  Notification insert are unchanged from Module 6.

  CONCURRENCY FIX (post-review): the ORIGINAL Module 7 version of this
  procedure trusted whatever @GovernmentPropertyReference/@VerificationStatus
  the caller passed in and just wrote them - ISNULL(@GovernmentPropertyReference,
  GovernmentPropertyReference) unconditionally. The caller's own duplicate
  check (usp_Property_FindByGovernmentPropertyReference, invoked from
  GovernmentDeedComparisonService.CompareAsync) runs as a SEPARATE database
  round trip, well before this procedure is ever called (OCR + comparison
  happen in between). That gap is a classic check-then-act race: two
  different PropertyIDs, resolving via two different DeedReferences to the
  SAME GovernmentPropertyReference, could both run their pre-check, both
  see "no existing holder" (because neither has written yet), and both
  then arrive here and both get written as the authoritative holder -
  exactly the scenario diagrammed in the review that raised this issue.
  Unlike the DeedReference duplicate check (which lives entirely inside
  usp_Property_Create - one procedure, one transaction, already
  concurrency-safe via sp_getapplock), the GovernmentPropertyReference
  check-then-write is split across a caller-side pre-check and a
  DB-side write. The two-step split itself is NOT removed here (the
  pre-check still exists and still gives an early, targeted duplicate
  report/message to the caller for the common non-racing case) - instead
  THIS procedure, the single write path for Property.GovernmentPropertyReference,
  becomes the actual authority: it re-derives, under an exclusive,
  transaction-scoped sp_getapplock keyed on the normalized reference,
  whether another PropertyID already holds it - RIGHT BEFORE writing,
  inside the same transaction as the write. Whichever of two concurrent
  callers acquires the lock first finds nothing and becomes the
  authoritative holder; the second, once the first COMMITs and releases
  the lock, finds the first's already-committed row and is downgraded to
  'DuplicateProperty' -> Disapproved HERE, regardless of what
  @VerificationStatus it originally arrived with, and its own
  GovernmentPropertyReference is left untouched (never persisted) - only
  the winner's row ever holds this reference. This exactly mirrors
  usp_Property_Create's own sp_getapplock pattern for DeedReference
  (@LockMode = 'Exclusive', @LockOwner = 'Transaction', so the lock
  releases automatically at COMMIT/ROLLBACK) but on a DIFFERENT lock
  resource namespace ('Property_GovRef_' vs 'Property_DeedReference_'),
  so the two locks can never collide with or block each other for
  unrelated deeds/references. Normalization mirrors DeedReference's own
  convention exactly: UPPER(LTRIM(RTRIM(...))) for the lock key and the
  EXISTS comparison; the ORIGINAL (non-normalized) value is what gets
  persisted into the column, exactly as usp_Property_Create persists the
  original @DeedReference rather than its normalized form. The EXISTS
  check excludes @PropertyID itself (PropertyID <> @PropertyID), so a
  property re-verifying against its own already-persisted reference is
  never flagged as a duplicate of itself - normal re-verification remains
  unaffected. No status filter on the EXISTS check (mirrors DeedReference
  again) - a Withdrawn/Disapproved/Rejected property still counts as
  already holding that government reference, so its deed cannot be
  "freed up" by withdrawing or disapproving it elsewhere. A schema-level
  UNIQUE constraint on GovernmentPropertyReference was considered and
  rejected for the same reason a UNIQUE index on DeedReference was
  rejected in this script's main header comment: the column is NULL for
  every row until this procedure resolves it, and - more importantly -
  proving it is safe against the live database's *existing* data has not
  been done, so an unproven blanket UNIQUE constraint is not added here;
  the procedural applock guard requires no such proof and protects every
  future write regardless.

  AUDIT-CONSISTENCY FIX (post-review, third pass): fixing the race above
  exposed a second, related problem - GovernmentDeedVerificationService
  used to persist the DeedVerification audit row (the caller's PRE-lock
  candidate verdict) BEFORE ever calling this procedure, so a race-loser
  downgrade happening HERE could leave a permanent audit row claiming
  "Verified"/"PriceAnomaly" for a property whose Status this procedure had
  just set to Disapproved for DuplicateProperty - the audit trail and the
  actual system verdict disagreeing, which is unacceptable for evidence
  that is supposed to explain the property's own status. The fix is on
  the Application side, not here: GovernmentDeedVerificationService now
  calls THIS procedure FIRST (before writing any audit row at all), reads
  back @EffectiveVerificationStatus, and if it differs from the candidate
  it started with, re-derives a corrected verdict (reusing
  GovernmentDeedFraudDetectionService.Classify itself, not a hand-rolled
  duplicate of its logic) before persisting the audit row exactly once,
  already correct - see that class's own "AUDIT-CONSISTENCY FIX" comment.
  This procedure's own transaction/lock behaviour is completely unchanged
  by that fix; only the OUTPUT parameter above was added so the caller can
  observe what actually happened.
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

    -- AUDIT-CONSISTENCY FIX (post-review, third pass): @EffectiveVerificationStatus
    -- tells the caller what this procedure ACTUALLY decided, which only
    -- ever differs from the @VerificationStatus it was called with in the
    -- one race-downgrade case below (Verified/PriceAnomaly -> DuplicateProperty
    -- under the applock). Initialised to the input value up front so every
    -- other branch (the overwhelming majority of calls) returns it
    -- unchanged with zero extra logic. GovernmentDeedVerificationService
    -- uses this to decide whether the DeedVerification audit row it is
    -- about to persist needs to reflect a different, corrected verdict
    -- than the pre-lock candidate it started with - see that class's own
    -- "AUDIT-CONSISTENCY FIX" comment for the full explanation of why the
    -- audit write now happens AFTER this call, not before.
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

    -- Set to 1 only for the two verdicts that ever resolved a trustworthy
    -- GovernmentPropertyReference in the first place (Verified, and a
    -- price-only PriceAnomaly) - see GovernmentDeedComparisonReport.
    -- GovernmentPropertyReference's own doc comment for exactly when the
    -- caller populates it. 'DuplicateProperty' never sets this: the
    -- caller already decided this run is the loser (or this procedure is
    -- about to decide that itself, below), so nothing new is persisted.
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

    -- Nothing to check/persist if this verdict never carried a usable
    -- reference in the first place (blank/NULL) - @PersistReference is
    -- forced back to 0 so the block below is skipped entirely.
    DECLARE @NormalizedGovRef NVARCHAR(50) = NULL;
    IF @PersistReference = 1 AND LTRIM(RTRIM(ISNULL(@GovernmentPropertyReference, N''))) <> N''
        SET @NormalizedGovRef = UPPER(LTRIM(RTRIM(@GovernmentPropertyReference)));
    ELSE
        SET @PersistReference = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @PersistReference = 1
        BEGIN
            -- AUTHORITATIVE, concurrency-safe duplicate check - see this
            -- procedure's own header comment ("CONCURRENCY FIX") for the
            -- full race explanation. Same sp_getapplock pattern as
            -- usp_Property_Create's DeedReference guard, different lock
            -- resource namespace so the two never collide.
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
                -- Another PropertyID already committed this reference
                -- while this run was still verifying (the exact race the
                -- caller-side pre-check cannot see) - this run loses.
                -- Downgrade to the duplicate outcome and do NOT persist
                -- the reference onto this PropertyID; only the winner
                -- ever holds it. @EffectiveVerificationStatus is
                -- overwritten here too (not just @NewStatus/@Message) so
                -- the caller can tell its original candidate verdict was
                -- overridden - see this procedure's own header parameter
                -- comment.
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

PRINT '>> usp_Property_ApplyDeedVerificationOutcome upgraded: DuplicateProperty mapping + concurrency-safe authoritative GovernmentPropertyReference check-and-persist under sp_getapplock (Module 7).';
GO
