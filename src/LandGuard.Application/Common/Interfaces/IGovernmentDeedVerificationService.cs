using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Phase 5B's top-level orchestrator for the Government Registry module:
/// Controller (Phase 5C, not yet wired) -&gt; this service -&gt;
/// <see cref="IGovernmentDeedComparisonService"/> -&gt;
/// <c>GovernmentDeedComparisonReport</c> -&gt;
/// <see cref="IGovernmentDeedFraudDetectionService"/> -&gt;
/// <c>GovernmentDeedFraudDetectionResult</c> -&gt; <c>DeedVerification</c>
/// persistence -&gt; Dapper -&gt; stored procedures -&gt; SQL Server - exactly the
/// pipeline this phase's own instructions diagram.
///
/// Deliberately a new interface/class, not an addition to
/// <c>GovernmentDeedComparisonService</c> or
/// <c>GovernmentDeedFraudDetectionService</c>: the former stays scoped to
/// comparison only (per its own doc comment, unmodified in this phase), and
/// the latter stays a pure, I/O-free classifier (per its own Phase 5A
/// architecture requirement, also unmodified) - persistence needed a new,
/// third seam rather than compromising either existing one.
/// </summary>
public interface IGovernmentDeedVerificationService
{
    /// <summary>
    /// Runs <see cref="IGovernmentDeedComparisonService.CompareAsync"/>,
    /// classifies the result via
    /// <see cref="IGovernmentDeedFraudDetectionService.Classify"/>, and
    /// persists both together as one new <c>DeedVerification</c> run (plus
    /// its field evidence and reasons) inside a single database
    /// transaction. Same parameters as
    /// <see cref="IGovernmentDeedComparisonService.CompareAsync"/> - see
    /// that interface's doc comment for the OCR/ownership/exception
    /// behavior this method inherits unchanged
    /// (<c>NotFoundException</c>/<c>UnauthorizedAccessException</c> for a
    /// nonexistent/not-owned <paramref name="propertyId"/>, a
    /// <see cref="Result{T}"/> failure for an OCR/upload problem - in
    /// either of the latter two cases, nothing is persisted).
    /// </summary>
    Task<Result<GovernmentDeedVerificationOutcome>> VerifyAndPersistAsync(
        int propertyId,
        string fileName,
        string contentType,
        Stream sellerDeedContent,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default);
}
