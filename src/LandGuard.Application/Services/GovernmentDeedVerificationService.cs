using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;

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

    public GovernmentDeedVerificationService(
        IGovernmentDeedComparisonService comparisonService,
        IGovernmentDeedFraudDetectionService fraudDetectionService,
        IGovernmentDeedVerificationStoredProcedures verificationStoredProcedures)
    {
        _comparisonService = comparisonService;
        _fraudDetectionService = fraudDetectionService;
        _verificationStoredProcedures = verificationStoredProcedures;
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

        var outcome = new GovernmentDeedVerificationOutcome
        {
            DeedVerificationId = deedVerificationId,
            FraudDetectionResult = fraudDetectionResult
        };

        return Result<GovernmentDeedVerificationOutcome>.Success(outcome);
    }
}
