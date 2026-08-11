using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;
using LandGuard.Domain.Enums;
using LandGuard.Domain.Exceptions;

namespace LandGuard.Application.Services;

/// <summary>
/// Implements <see cref="IGovernmentDeedVerificationService"/> - Government
/// Registry module, Phase 5B. Orchestrates
/// <see cref="IGovernmentDeedComparisonService"/> (Phase 4, unmodified) -&gt;
/// <see cref="IGovernmentDeedFraudDetectionService"/> (Phase 5A, unmodified)
/// -&gt; <see cref="IGovernmentDeedVerificationStoredProcedures.PersistAsync"/>
/// (Infrastructure) for the actual database write.
///
/// Mandatory Deed / Form-vs-Deed Verification requirement: after
/// persisting, this class also applies the automated Property.Status
/// transition the verdict implies (Approved/Disapproved/Pending - see
/// <see cref="VerifyAndPersistAsync"/>'s own inline comment for the exact
/// mapping and why Unverified/UnverifiedCancelled are skipped) via
/// <see cref="IPropertyStoredProcedures.ApplyDeedVerificationOutcomeAsync"/>.
///
/// This class says only "persist this verification result" -
/// <see cref="IGovernmentDeedVerificationStoredProcedures.PersistAsync"/> is
/// a single atomic operation from this class's point of view; transaction
/// ownership (BEGIN/COMMIT/ROLLBACK) lives entirely inside its
/// Infrastructure implementation
/// (<c>GovernmentDeedVerificationStoredProcedures</c>), which is the only
/// place any EF Core transaction type is ever touched. This class - and
/// the whole Application layer - has no dependency on
/// <c>DatabaseFacade</c>, <c>IDbContextTransaction</c>, or any other EF
/// Core type beyond the plain <c>DbSet&lt;T&gt;</c> exposure
/// <see cref="IApplicationDbContext"/> already needed for Module 2.
/// </summary>
public class GovernmentDeedVerificationService : IGovernmentDeedVerificationService
{
    private readonly IGovernmentDeedComparisonService _comparisonService;
    private readonly IGovernmentDeedFraudDetectionService _fraudDetectionService;
    private readonly IGovernmentDeedVerificationStoredProcedures _verificationStoredProcedures;
    private readonly IPropertyStoredProcedures _propertyStoredProcedures;

    private static readonly string AdminRoleValue = UserRole.Administrator.ToDbValue();

    public GovernmentDeedVerificationService(
        IGovernmentDeedComparisonService comparisonService,
        IGovernmentDeedFraudDetectionService fraudDetectionService,
        IGovernmentDeedVerificationStoredProcedures verificationStoredProcedures,
        IPropertyStoredProcedures propertyStoredProcedures)
    {
        _comparisonService = comparisonService;
        _fraudDetectionService = fraudDetectionService;
        _verificationStoredProcedures = verificationStoredProcedures;
        _propertyStoredProcedures = propertyStoredProcedures;
    }

    public async Task<Result<GovernmentDeedVerificationOutcome>> VerifyAndPersistAsync(
        int propertyId,
        string fileName,
        string contentType,
        Stream sellerDeedContent,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default)
    {
        // Ownership/ID-spoofing protection is entirely inherited from
        // GovernmentDeedComparisonService.CompareAsync (unmodified in this
        // phase) - it throws NotFoundException/UnauthorizedAccessException
        // for a nonexistent/not-owned propertyId before anything else runs.
        // Nothing here re-checks ownership a second time or moves it into
        // the database, per this phase's own "authorization stays an
        // Application-layer responsibility" instruction.
        var comparisonResult = await _comparisonService.CompareAsync(
            propertyId, fileName, contentType, sellerDeedContent, callerId, callerRole, cancellationToken);

        if (!comparisonResult.Succeeded)
        {
            // An OCR/upload failure - nothing was produced to classify or
            // persist. Matches GovernmentDeedComparisonService's own
            // "Result failure, not an exception" treatment for this case.
            return Result<GovernmentDeedVerificationOutcome>.Failure(comparisonResult.Errors);
        }

        var report = comparisonResult.Data!;

        // Pure, synchronous, no I/O - see GovernmentDeedFraudDetectionService's
        // own doc comment. Never touches the database itself. This is the
        // CANDIDATE verdict only - see the AUDIT-CONSISTENCY FIX comment
        // below for why it is not persisted to DeedVerification yet.
        var candidateResult = _fraudDetectionService.Classify(report);

        // AUDIT-CONSISTENCY FIX (post-review, third pass): this call now
        // runs BEFORE any DeedVerification audit row is persisted - the
        // OLD order called PersistAsync (the audit write) first, using the
        // pre-lock candidateResult. usp_Property_ApplyDeedVerificationOutcome
        // is the single authoritative, lock-protected finalization
        // decision for Property.Status (see that procedure's own
        // "CONCURRENCY FIX" comment): it can silently downgrade a
        // candidate Verified/PriceAnomaly verdict to DuplicateProperty if
        // this run loses a race for the same GovernmentPropertyReference.
        // Persisting the audit row before knowing that outcome risked a
        // permanent DeedVerification row claiming "Verified" for a
        // property this call had just set to Disapproved - the audit
        // trail and the actual system verdict disagreeing, which is
        // unacceptable for evidence that is supposed to explain the
        // property's own status.
        //
        // A full merge of this call and the audit INSERTs into one T-SQL
        // transaction was considered and rejected as more than this fix
        // needs: usp_Property_ApplyDeedVerificationOutcome owns its own
        // BEGIN/COMMIT/ROLLBACK, and T-SQL's unqualified ROLLBACK inside a
        // nested transaction unwinds the OUTER transaction too - nesting
        // GovernmentDeedVerificationStoredProcedures.PersistAsync's EF
        // transaction around this call without rewriting that procedure's
        // own transaction-ownership pattern would risk exactly the kind of
        // correctness regression this whole fix exists to remove. The
        // reordering below already fully closes the audit/Property.Status
        // disagreement without that larger, riskier restructuring - see
        // this class's own header doc comment for why Application-layer
        // code deliberately never opens its own EF transaction here either
        // way.
        //
        // CORRECTED business rule (unchanged from before): this call
        // covers EVERY DeedVerificationStatus value Classify can produce,
        // including Unverified/UnverifiedCancelled - see those two enum
        // members' own doc comments for why. Both represent
        // IGovernmentRegistryService successfully answering "no such
        // record" / "this record is cancelled" (that interface's own doc
        // comment: "returns null - never throws - when no record
        // matches"), i.e. a genuine authoritative negative finding, not a
        // technical failure. A real technical failure (registry service
        // unavailable, network error, timeout, unexpected API failure) is
        // a thrown exception from that interface - it propagates out of
        // GovernmentDeedComparisonService.CompareAsync uncaught, so
        // Classify (and therefore this call) is never reached at all for
        // that case; Property.Status is left exactly where it was. The
        // seller's own deed OCR failing is handled the same way one level
        // up - see CompareAsync's ocrResult.Succeeded check, which also
        // returns before Classify is ever invoked.
        //
        // Deliberately NOT best-effort/swallowed: if this call throws
        // (e.g. the Withdrawn guard), nothing has been persisted anywhere
        // yet for this attempt - no DeedVerification audit row exists
        // either, since that write now happens after this succeeds. A
        // failed attempt therefore leaves NO row instead of a wrong one;
        // an absent audit row can never disagree with Property.Status,
        // only an incorrect one could.
        var effectiveVerificationStatus = candidateResult.Status.ToString();

        if (candidateResult.Status is DeedVerificationStatus.Verified
            or DeedVerificationStatus.FormMismatch
            or DeedVerificationStatus.Fraudulent
            or DeedVerificationStatus.PriceAnomaly
            or DeedVerificationStatus.Unverified
            or DeedVerificationStatus.UnverifiedCancelled
            or DeedVerificationStatus.DuplicateProperty)
        {
            // Global Duplicate-Property Prevention requirement:
            // report.GovernmentPropertyReference is passed through
            // unchanged so usp_Property_ApplyDeedVerificationOutcome can
            // persist it onto Property.GovernmentPropertyReference in the
            // same write as the status change - see that report property's
            // own doc comment for exactly when it is non-null.
            var (_, resolvedStatus) = await _propertyStoredProcedures.ApplyDeedVerificationOutcomeAsync(
                report.PropertyId, candidateResult.Status.ToString(), candidateResult.Summary,
                report.GovernmentPropertyReference, cancellationToken);

            effectiveVerificationStatus = resolvedStatus;
        }

        // If (and only if) the authoritative call above downgraded the
        // verdict, re-derive a CORRECTED result before persisting anything -
        // reusing GovernmentDeedFraudDetectionService.Classify's own
        // "DuplicateProperty" branch (BuildResult + the fixed BuildSummary
        // wording) rather than hand-rolling a duplicate of that
        // Reasons/Summary construction. report.OverallOutcome is the only
        // input that branch reads; report is not read for its
        // OverallOutcome again anywhere below (only PropertyId/
        // GovernmentPropertyReference, both unaffected by this mutation),
        // so overwriting it here is safe.
        var finalResult = candidateResult;
        if (!string.Equals(effectiveVerificationStatus, candidateResult.Status.ToString(), StringComparison.Ordinal))
        {
            report.OverallOutcome = "DuplicateProperty";
            finalResult = _fraudDetectionService.Classify(report);
        }

        // A single atomic call - see this class's own doc comment for why
        // no transaction is opened, held, or referenced here. Persisted
        // exactly once, already reflecting the FINAL effective verdict -
        // never the pre-lock candidate - and never updated/deleted
        // afterward, preserving the append-only DeedVerification history
        // principle unchanged.
        var deedVerificationId = await _verificationStoredProcedures.PersistAsync(
            finalResult, callerId, cancellationToken);

        // PHASE E: re-run the legacy supporting-risk engine now that a real
        // seller deed document is persisted, so its MISSING_INFO rule (see
        // usp_Fraud_AnalyseProperty's own Phase E note) stops reporting a
        // missing deed the moment one actually exists, instead of only on
        // the next unrelated edit. Reuses the exact same
        // IPropertyStoredProcedures.AnalyseAsync wrapper Property Create/
        // Update already call - no new stored procedure, no duplicated
        // logic. usp_Fraud_AnalyseProperty -> usp_Risk_GenerateReport never
        // writes Property.Status (Phase C), so this cannot change the
        // property's listing status; it only refreshes FraudCheck/
        // RiskReport. Best-effort: a failure here must not undo the
        // verification that was just successfully persisted above, so it
        // is swallowed rather than propagated as this call's own failure.
        try
        {
            await _propertyStoredProcedures.AnalyseAsync(report.PropertyId, cancellationToken);
        }
        catch
        {
            // Supporting risk indicators simply stay at their previous
            // (now slightly stale) values until the next successful
            // analysis run - never a reason to fail deed verification,
            // which already succeeded and persisted above.
        }

        var outcome = new GovernmentDeedVerificationOutcome
        {
            DeedVerificationId = deedVerificationId,
            // finalResult, not candidateResult - the caller (Seller-facing
            // API response) must see the same FINAL verdict that was just
            // persisted and applied to Property.Status, never a stale
            // pre-lock candidate that the duplicate-reference check went
            // on to override.
            FraudDetectionResult = finalResult
        };

        return Result<GovernmentDeedVerificationOutcome>.Success(outcome);
    }

    public async Task<Result<IReadOnlyList<DeedVerificationHistoryEntry>>> GetHistoryAsync(
        int propertyId, int callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        // Same ownership check GovernmentDeedComparisonService.CompareAsync
        // applies (strict ownership against the raw property, not the
        // "or Approved/public" visibility rule GetPropertyByIdAsync's own
        // public read path uses) - a Seller may only read their own
        // property's verification history, an Admin may read any, and a
        // nonexistent id 404s the same way for everyone.
        var property = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (property is null)
        {
            throw new NotFoundException("Property not found.");
        }

        if (property.Listing.SellerId != callerId && !IsAdmin(callerRole))
        {
            throw new UnauthorizedAccessException();
        }

        var history = await _verificationStoredProcedures.GetHistoryAsync(propertyId, cancellationToken);

        return Result<IReadOnlyList<DeedVerificationHistoryEntry>>.Success(history);
    }

    private static bool IsAdmin(string? callerRole) => string.Equals(callerRole, AdminRoleValue, StringComparison.Ordinal);
}
