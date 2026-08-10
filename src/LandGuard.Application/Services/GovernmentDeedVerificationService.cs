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
        // own doc comment. Never touches the database itself.
        var fraudDetectionResult = _fraudDetectionService.Classify(report);

        // A single atomic call - see this class's own doc comment for why
        // no transaction is opened, held, or referenced here.
        var deedVerificationId = await _verificationStoredProcedures.PersistAsync(
            fraudDetectionResult, callerId, cancellationToken);

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
            FraudDetectionResult = fraudDetectionResult
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
