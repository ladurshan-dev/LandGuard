using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Classifies an already-produced <see cref="GovernmentDeedComparisonReport"/>
/// (Government Registry module, Phase 4) into a
/// <see cref="GovernmentDeedFraudDetectionResult"/> verdict - Phase 5A.
///
/// Deliberately synchronous, not <c>Task</c>-returning like every other
/// service interface in this solution: this is a pure interpretation of
/// data the caller already has in memory, with no HTTP, no SQL/Dapper, no
/// filesystem access and no OCR call of its own (those all already happened
/// to produce the <see cref="GovernmentDeedComparisonReport"/> being
/// classified) - see <see cref="Classify"/>'s doc comment for the full
/// reasoning. Making it synchronous is what keeps it trivially unit-testable
/// with a hand-built report and no mocks, which the async, I/O-composing
/// shape every other service in this solution uses would not offer any
/// benefit for here.
///
/// Implemented by <c>GovernmentDeedFraudDetectionService</c>. Registered in
/// <c>Application.DependencyInjection</c> alongside
/// <see cref="IGovernmentDeedComparisonService"/> for forward compatibility
/// with the endpoint(s) a later phase will add, even though no controller
/// consumes it yet in Phase 5A (see that registration's own comment).
/// </summary>
public interface IGovernmentDeedFraudDetectionService
{
    /// <summary>
    /// Interprets <paramref name="report"/> and returns the resulting
    /// verdict. Never throws for any valid <see cref="GovernmentDeedComparisonReport"/>
    /// shape <c>GovernmentDeedComparisonService.CompareAsync</c> can
    /// actually produce - every one of its three <c>OverallOutcome</c>
    /// values ("Clean", "Mismatch", "MissingOrCancelledGovernmentRecord")
    /// maps to exactly one <see cref="DTOs.DeedComparison.GovernmentDeedFraudDetectionResult.Status"/>.
    /// </summary>
    GovernmentDeedFraudDetectionResult Classify(GovernmentDeedComparisonReport report);
}
