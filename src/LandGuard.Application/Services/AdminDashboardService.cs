using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Admin;
using LandGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LandGuard.Application.Services;

/// <inheritdoc cref="IAdminDashboardService" />
public class AdminDashboardService : IAdminDashboardService
{
    private readonly IApplicationDbContext _context;

    public AdminDashboardService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminFraudDashboardResponse>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        // Keyless, always-exactly-one-row view (every column is an
        // independent scalar subquery) - see FraudStatistics's own doc
        // comment. Backs both Users and the legacy Risk section below.
        var stats = await _context.FraudStatistics
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        var users = new AdminDashboardUserStatistics
        {
            TotalBuyers = stats.TotalBuyers,
            TotalSellers = stats.TotalSellers,
            VerifiedSellers = stats.VerifiedSellers,
            SuspendedUsers = stats.SuspendedUsers
        };

        var risk = new AdminDashboardRiskStatistics
        {
            Low = stats.LowRiskCount,
            Medium = stats.MediumRiskCount,
            High = stats.HighRiskCount,
            AverageRiskScore = stats.AverageRiskScore
        };

        var properties = await GetPropertyStatisticsAsync(cancellationToken);
        var deedVerification = await GetDeedVerificationStatisticsAsync(cancellationToken);
        var ruleTriggers = await GetRuleTriggersAsync(cancellationToken);
        var reviewProperties = await GetReviewPropertiesAsync(cancellationToken);

        var response = new AdminFraudDashboardResponse
        {
            Users = users,
            Properties = properties,
            DeedVerification = deedVerification,
            Risk = risk,
            RuleTriggers = ruleTriggers,
            ReviewProperties = reviewProperties
        };

        return Result<AdminFraudDashboardResponse>.Success(response);
    }

    /// <summary>
    /// Current Property.Status counts, computed live rather than reused
    /// from vw_FraudStatistics (that view predates Withdrawn/Disapproved -
    /// see AdminDashboardPropertyStatistics's own doc comment). A single
    /// GROUP BY/COUNT query (standard, well-supported EF Core
    /// translation) - the enum comparison below uses PropertyStatus
    /// itself (the same type/converter PropertyConfiguration already
    /// applies to Property.Status), never a guessed numeric/string
    /// literal.
    /// </summary>
    private async Task<AdminDashboardPropertyStatistics> GetPropertyStatisticsAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.Properties
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = counts.ToDictionary(c => c.Status, c => c.Count);

        int CountOf(PropertyStatus status) => byStatus.TryGetValue(status, out var count) ? count : 0;

        return new AdminDashboardPropertyStatistics
        {
            Total = counts.Sum(c => c.Count),
            Approved = CountOf(PropertyStatus.Approved),
            Pending = CountOf(PropertyStatus.Pending),
            Flagged = CountOf(PropertyStatus.Flagged),
            Rejected = CountOf(PropertyStatus.Rejected),
            Withdrawn = CountOf(PropertyStatus.Withdrawn),
            Disapproved = CountOf(PropertyStatus.Disapproved)
        };
    }

    /// <summary>
    /// Counts only the LATEST dbo.DeedVerification row per PropertyID.
    /// dbo.DeedVerification is append-only history - a corrected
    /// re-verification inserts a new row, it never edits an old one (see
    /// DeedVerification's own doc comment) - so counting every historical
    /// row would inflate the dashboard with superseded verdicts.
    ///
    /// GroupBy(PropertyId).Select(g => g.OrderByDescending(...).First())
    /// is EF Core's standard "latest row per group" translation (a
    /// correlated per-group TOP(1), evaluated database-side, not a
    /// client-side loop over every history row). The ordering
    /// (VerifiedDate DESC, then DeedVerificationID DESC) matches this
    /// project's own established "most recent run" tie-break -
    /// IX_DeedVerification_Property_Date is keyed the same way, and
    /// GovernmentDeedVerificationService's own history reads use the
    /// identical ordering. Only PropertyId+VerificationStatus is
    /// projected out of the group - Summary/GovernmentRecordId/
    /// SellerDocumentReference and every other column are never touched
    /// by this query.
    ///
    /// The final per-status tally then runs over this already-deduplicated,
    /// at-most-one-row-per-property in-memory list (its size is bounded by
    /// the number of properties ever verified, never by the number of
    /// historical verification runs) - the one piece of this method that
    /// happens in C# rather than SQL, kept deliberately small and
    /// documented here rather than composing a second nested GroupBy back
    /// into the same IQueryable, whose translation is far less
    /// predictable for this provider/version without a live database to
    /// verify it against.
    ///
    /// Technical OCR/registry failures are never counted here at all -
    /// they never produce a DeedVerification row in the first place.
    /// GovernmentDeedComparisonService only reaches
    /// usp_DeedVerification_Create once a definitive verdict exists; a
    /// genuine technical failure (network/OCR error) is a thrown
    /// exception that propagates out before any row - or status - is ever
    /// produced (see DeedVerificationStatus.Unverified's own doc comment).
    /// </summary>
    private async Task<AdminDashboardDeedVerificationStatistics> GetDeedVerificationStatisticsAsync(CancellationToken cancellationToken)
    {
        var latestPerProperty = await _context.DeedVerifications
            .AsNoTracking()
            .GroupBy(v => v.PropertyId)
            .Select(g => g
                .OrderByDescending(v => v.VerifiedDate)
                .ThenByDescending(v => v.DeedVerificationId)
                .Select(v => new { v.PropertyId, v.VerificationStatus })
                .First())
            .ToListAsync(cancellationToken);

        var byStatus = latestPerProperty
            .GroupBy(v => v.VerificationStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        int CountOf(DeedVerificationStatus status) => byStatus.TryGetValue(status, out var count) ? count : 0;

        return new AdminDashboardDeedVerificationStatistics
        {
            Total = latestPerProperty.Count,
            Verified = CountOf(DeedVerificationStatus.Verified),
            FormMismatch = CountOf(DeedVerificationStatus.FormMismatch),
            Fraudulent = CountOf(DeedVerificationStatus.Fraudulent),
            PriceAnomaly = CountOf(DeedVerificationStatus.PriceAnomaly),
            DuplicateProperty = CountOf(DeedVerificationStatus.DuplicateProperty),
            Unverified = CountOf(DeedVerificationStatus.Unverified),
            UnverifiedCancelled = CountOf(DeedVerificationStatus.UnverifiedCancelled)
        };
    }

    /// <summary>
    /// Every row of dbo.vw_FraudStatistics's sibling rule-frequency view,
    /// unmodified - no rule is recalculated in C#, this is a straight
    /// projection in the requested order (TimesTriggered DESC, then
    /// RuleName).
    /// </summary>
    private async Task<IReadOnlyList<AdminDashboardRuleTriggerItem>> GetRuleTriggersAsync(CancellationToken cancellationToken)
    {
        return await _context.RuleTriggerFrequencies
            .AsNoTracking()
            .OrderByDescending(r => r.TimesTriggered)
            .ThenBy(r => r.RuleName)
            .Select(r => new AdminDashboardRuleTriggerItem
            {
                RuleCode = r.RuleCode,
                RuleName = r.RuleName,
                Weight = r.Weight,
                TimesTriggered = r.TimesTriggered,
                TimesEvaluated = r.TimesEvaluated,
                TriggerRatePercent = r.TriggerRatePercent
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Top 10 (RiskScore DESC, UploadDate ASC - the same ordering
    /// IAdminModerationService.GetReviewQueueAsync already uses for the
    /// full review queue) mapped to the minimal, privacy-safe summary DTO.
    /// dbo.vw_FlaggedProperty (FlaggedProperty) itself is never returned
    /// to the API - only the seven fields below are ever selected, so
    /// SellerNIC/OwnerNIC/DeedReference/OCR/registry/contact fields never
    /// leave this query even though the source view carries some of them.
    /// </summary>
    private async Task<IReadOnlyList<AdminDashboardReviewPropertySummary>> GetReviewPropertiesAsync(CancellationToken cancellationToken)
    {
        return await _context.FlaggedProperties
            .AsNoTracking()
            .OrderByDescending(p => p.RiskScore)
            .ThenBy(p => p.UploadDate)
            .Take(10)
            .Select(p => new AdminDashboardReviewPropertySummary
            {
                PropertyId = p.PropertyId,
                Title = p.Title,
                Status = p.Status,
                RiskScore = p.RiskScore,
                RiskLevel = p.RiskLevel,
                DaysWaiting = p.DaysWaiting,
                OpenReportCount = p.OpenReportCount
            })
            .ToListAsync(cancellationToken);
    }
}
