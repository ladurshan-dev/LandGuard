namespace LandGuard.Application.Common.Models;

/// <summary>One row of <c>dbo.DeedVerificationReason</c>, as returned by <c>usp_DeedVerification_GetHistory</c>'s third result set.</summary>
public class DeedVerificationReasonRecord
{
    public int DeedVerificationReasonId { get; set; }

    public int DeedVerificationId { get; set; }

    /// <summary>DeedFraudReason's exact string name.</summary>
    public string Reason { get; set; } = null!;
}
