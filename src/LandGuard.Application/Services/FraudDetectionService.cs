using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Fraud;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Services;

/// <summary>
/// Orchestrates fraud analysis, reporting and history for Module 5A. No
/// SQL, no HTTP, no fraud rule logic - it composes
/// <see cref="IPropertyStoredProcedures"/> (to trigger analysis and to
/// read the raw property for ownership checks),
/// <see cref="IPropertyService"/> (to read a property + its report through
/// the same visibility rule PropertyController already exposes),
/// <see cref="IUserStoredProcedures"/> (to check the owning seller's
/// active status) and <see cref="IFraudStoredProcedures"/> (the one new
/// capability, history). This mirrors <see cref="AuthService"/> and
/// <see cref="PropertyService"/>'s shape exactly.
///
/// Two different authorization checks are used, deliberately not the same
/// one, because "who can analyze" and "who can read" are genuinely
/// different rules here:
/// - <see cref="AnalyzePropertyAsync"/> uses a strict ownership check
///   (owning Seller or Admin only) against the raw property from
///   <c>IPropertyStoredProcedures.GetByIdAsync</c> - reusing
///   <c>IPropertyService.GetByIdAsync</c>'s visibility rule here would
///   wrongly let any Seller trigger analysis on any other Seller's
///   already-Approved (therefore publicly visible) listing.
/// - <see cref="GetFraudReportAsync"/>/<see cref="GetFraudHistoryAsync"/>/
///   <see cref="CalculateRiskScoreAsync"/> reuse
///   <c>IPropertyService.GetByIdAsync</c> directly, which already
///   implements exactly "Approved is public, otherwise owner or Admin
///   only" - the same rule a Buyer's read-only access should follow.
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IPropertyStoredProcedures _propertyStoredProcedures;
    private readonly IPropertyService _propertyService;
    private readonly IUserStoredProcedures _userStoredProcedures;
    private readonly IFraudStoredProcedures _fraudStoredProcedures;

    private static readonly string AdminRoleValue = UserRole.Administrator.ToDbValue();

    public FraudDetectionService(
        IPropertyStoredProcedures propertyStoredProcedures,
        IPropertyService propertyService,
        IUserStoredProcedures userStoredProcedures,
        IFraudStoredProcedures fraudStoredProcedures)
    {
        _propertyStoredProcedures = propertyStoredProcedures;
        _propertyService = propertyService;
        _userStoredProcedures = userStoredProcedures;
        _fraudStoredProcedures = fraudStoredProcedures;
    }

    public async Task<Result<FraudAnalysisResponse>> AnalyzePropertyAsync(
        int propertyId, int callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        // Property exists.
        var existing = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (existing is null)
        {
            return Result<FraudAnalysisResponse>.Failure("Property not found.");
        }

        // Seller owns property (or caller is an Admin) - a strict
        // ownership check, not the "or Approved/public" visibility rule
        // GetFraudReportAsync uses (see this class's doc comment).
        var isOwner = existing.Listing.SellerId == callerId;
        if (!isOwner && !IsAdmin(callerRole))
        {
            return Result<FraudAnalysisResponse>.Failure("Only the property's owner or an administrator may trigger fraud analysis.");
        }

        // Property (seller) is active. dbo.Property has no IsActive column
        // of its own - "active" for a listing means its owning seller
        // hasn't been suspended, exactly the definition
        // vw_PublishedProperty and usp_Fraud_AnalyseProperty's own NIC
        // check (Users.IsActive) already use.
        var seller = await _userStoredProcedures.GetByIdAsync(existing.Listing.SellerId, cancellationToken);
        if (seller is null || !seller.IsActive)
        {
            return Result<FraudAnalysisResponse>.Failure("The property's seller account is inactive; analysis is unavailable.");
        }

        // usp_Fraud_AnalyseProperty already exists (Module 2) and is
        // already triggered by Property Create/Update (Module 4) via this
        // same wrapper - reused here, not duplicated.
        await _propertyStoredProcedures.AnalyseAsync(propertyId, cancellationToken);

        var refreshed = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);

        return Result<FraudAnalysisResponse>.Success(BuildAnalysisResponse(refreshed!));
    }

    public async Task<Result<RiskSummaryResponse>> CalculateRiskScoreAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        // "Calculates" only in the sense of reading what
        // usp_Risk_GenerateReport already calculated and stored - never
        // recomputed here (see this class's doc comment).
        var result = await _propertyService.GetByIdAsync(propertyId, callerId, callerRole, cancellationToken);

        return result.Succeeded
            ? Result<RiskSummaryResponse>.Success(BuildRiskSummary(result.Data!.Listing))
            : Result<RiskSummaryResponse>.Failure(result.Errors);
    }

    public async Task<Result<FraudReportResponse>> GetFraudReportAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        var result = await _propertyService.GetByIdAsync(propertyId, callerId, callerRole, cancellationToken);

        return result.Succeeded
            ? Result<FraudReportResponse>.Success(BuildReportResponse(result.Data!))
            : Result<FraudReportResponse>.Failure(result.Errors);
    }

    public async Task<Result<FraudHistoryResponse>> GetFraudHistoryAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        // Same visibility rule as GetFraudReportAsync applies before
        // history is revealed - a Buyer probing a Pending/Flagged/
        // Rejected listing that isn't theirs gets the identical "not
        // found" this call already produces for a nonexistent id.
        var visibility = await _propertyService.GetByIdAsync(propertyId, callerId, callerRole, cancellationToken);
        if (!visibility.Succeeded)
        {
            return Result<FraudHistoryResponse>.Failure(visibility.Errors);
        }

        var history = await _fraudStoredProcedures.GetHistoryAsync(propertyId, cancellationToken);

        var response = new FraudHistoryResponse
        {
            PropertyId = propertyId,
            Runs = history.Select(BuildHistoryEntry).ToList()
        };

        return Result<FraudHistoryResponse>.Success(response);
    }

    private static RiskSummaryResponse BuildRiskSummary(PropertyListingResult listing) => new()
    {
        RiskScore = listing.RiskScore ?? 0,
        RiskLevel = listing.RiskLevel,
        FraudStatus = listing.FraudStatus,
        Summary = listing.RiskSummary,
        GeneratedDate = listing.RiskGeneratedDate
    };

    private static FraudRuleResponse MapRule(PropertyFraudRuleResult rule) => new()
    {
        RuleCode = rule.RuleCode,
        RuleName = rule.RuleName,
        Weight = rule.MaxPoints,
        Passed = !rule.Triggered,
        Message = rule.Description
    };

    private static FraudAnalysisResponse BuildAnalysisResponse(PropertyDetail detail) => new()
    {
        PropertyId = detail.Listing.PropertyId,
        PropertyStatus = detail.Listing.Status,
        Risk = BuildRiskSummary(detail.Listing),
        Rules = detail.FraudReport.Select(MapRule).ToList()
    };

    private static FraudReportResponse BuildReportResponse(PropertyDetail detail) => new()
    {
        PropertyId = detail.Listing.PropertyId,
        PropertyTitle = detail.Listing.Title,
        PropertyStatus = detail.Listing.Status,
        Risk = BuildRiskSummary(detail.Listing),
        Rules = detail.FraudReport.Select(MapRule).ToList()
    };

    private static FraudHistoryEntryResponse BuildHistoryEntry(FraudHistoryEntry entry) => new()
    {
        FraudCheckId = entry.FraudCheckId,
        CheckDate = entry.CheckDate,
        Risk = new RiskSummaryResponse
        {
            RiskScore = entry.RiskScore ?? 0,
            RiskLevel = entry.RiskLevel ?? "Low",
            FraudStatus = entry.FraudStatus,
            Summary = entry.Summary,
            GeneratedDate = entry.GeneratedDate
        }
    };

    private static bool IsAdmin(string? callerRole) => string.Equals(callerRole, AdminRoleValue, StringComparison.Ordinal);
}
