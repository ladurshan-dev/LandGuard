using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Admin;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Read-only reporting for the Admin Fraud Dashboard - deliberately its
/// own service, kept separate from every other Admin-facing service in
/// this solution:
/// <list type="bullet">
/// <item><description><c>FraudDetectionService</c> performs the numeric,
/// weighted-rule fraud analysis for a SINGLE property (writes
/// <c>FraudCheck</c>/<c>RiskReport</c>); this service never analyses
/// anything or writes to the database, it only aggregates figures that
/// analysis has already produced.</description></item>
/// <item><description><c>IAdminModerationService</c> is the manual
/// approve/reject workflow for one property at a time; this service never
/// changes a <c>Property.Status</c>.</description></item>
/// </list>
///
/// Every section is sourced from infrastructure that already exists -
/// <c>IApplicationDbContext.FraudStatistics</c> (<c>dbo.vw_FraudStatistics</c>),
/// <c>.Properties</c>, <c>.DeedVerifications</c>, <c>.RuleTriggerFrequencies</c>
/// (<c>dbo.vw_RuleTriggerFrequency</c>) and <c>.FlaggedProperties</c>
/// (<c>dbo.vw_FlaggedProperty</c>) - no new SQL, no new stored procedure,
/// no change to <c>IStoredProcedureExecutor</c>. All reads are
/// <c>AsNoTracking()</c>.
///
/// <b>Property status counts</b> are computed live from
/// <c>IApplicationDbContext.Properties</c> rather than
/// <c>vw_FraudStatistics</c>'s own Approved/Pending/Flagged/Rejected
/// columns, because that view predates <c>PropertyStatus.Withdrawn</c>/
/// <c>Disapproved</c> and this dashboard needs both.
///
/// <b>Deed verification counts</b> reflect only the LATEST
/// <c>DeedVerification</c> row per PropertyID - see
/// <c>AdminDashboardService.GetDeedVerificationStatisticsAsync</c> for the
/// exact query and why (that table is append-only history, so counting
/// every row would inflate the dashboard with superseded verdicts).
///
/// <b>Risk vs deed verification</b> are reported as two separate DTO
/// sections and must never be combined/conflated by a caller - High Risk
/// does not mean Fraudulent, Low Risk does not mean Verified. They are
/// independent systems (see <c>DeedVerificationStatus</c>'s own doc
/// comment on why the numeric fraud engine and the government
/// deed-verification system are deliberately kept apart).
/// </summary>
public interface IAdminDashboardService
{
    Task<Result<AdminFraudDashboardResponse>> GetDashboardAsync(CancellationToken cancellationToken = default);
}
