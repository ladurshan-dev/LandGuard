using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.AdminAction</c> [ext] - the audit trail of every
/// administrative decision (FR09/NFR02). Has three independent optional
/// FKs (a listing acted on, a user acted on, a report acted on) because
/// one admin action typically targets exactly one of those, never all
/// three. Every <c>usp_Admin_*</c> procedure inserts exactly one row here.
/// </summary>
public class AdminAction
{
    public int AdminActionId { get; set; }

    public int AdminId { get; set; }

    public AdminActionType ActionType { get; set; }

    public int? PropertyId { get; set; }

    public int? TargetUserId { get; set; }

    /// <summary>References dbo.SuspiciousReport.SuspiciousReportID.</summary>
    public int? ReportId { get; set; }

    public string? Remarks { get; set; }

    public DateTime ActionDate { get; set; }

    // Navigation properties -------------------------------------------------

    /// <summary>The administrator who performed the action.</summary>
    public User Admin { get; set; } = null!;

    public Property? Property { get; set; }

    /// <summary>The user account this action targeted (e.g. SuspendUser, VerifyNIC).</summary>
    public User? TargetUser { get; set; }

    public SuspiciousReport? SuspiciousReport { get; set; }
}
