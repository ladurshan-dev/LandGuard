using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Fraud;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Service Layer contract for Module 5C (OCR-Based Fraud Comparison).
/// DocumentComparisonController depends only on this interface, never on
/// DocumentComparisonService directly or on any of the stored-procedure
/// abstractions it composes - the same shape IFraudDetectionService/
/// IPropertyService established.
///
/// This is a new service, composing <c>IFraudDetectionService</c> (rather
/// than adding methods onto it) - consistent with how Module 5A itself
/// composed <c>IPropertyService</c> instead of modifying it: it reuses the
/// existing Fraud Detection Foundation's read path
/// (<c>CalculateRiskScoreAsync</c>) to show the current fraud risk
/// alongside a comparison, without editing a completed Module 5A file or
/// introducing a second, competing fraud engine.
/// </summary>
public interface IDocumentComparisonService
{
    /// <summary>
    /// Compares already-produced OCR field data (Module 5B's output,
    /// supplied in <paramref name="request"/> - OCR is not re-run here)
    /// against the property's LandGuardDB records, persists the result via
    /// usp_DocumentComparison_Save, and returns it alongside the
    /// property's current fraud risk. Validates the property exists, the
    /// caller owns it (or is an Admin) - the same strict ownership check
    /// AnalyzePropertyAsync uses - and the owning seller's account is
    /// active.
    /// </summary>
    Task<Result<DocumentComparisonResponse>> CompareDocumentAsync(
        int propertyId, DocumentComparisonRequest request, int callerId, string? callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the most recent comparison for a property - never re-compares.
    /// Subject to the same visibility rule as
    /// IFraudDetectionService.GetFraudReportAsync (public once Approved,
    /// otherwise owner or Admin only) - a Buyer's read-only access.
    /// </summary>
    Task<Result<DocumentComparisonResponse>> GetLatestComparisonAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default);
}
