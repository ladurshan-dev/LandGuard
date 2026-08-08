namespace LandGuard.Application.Common.Models;

/// <summary>One row of <c>dbo.DeedVerificationField</c>, as returned by <c>usp_DeedVerification_GetHistory</c>'s second result set.</summary>
public class DeedVerificationFieldRecord
{
    public int DeedVerificationFieldId { get; set; }

    public int DeedVerificationId { get; set; }

    public string FieldName { get; set; } = null!;

    public string? GovernmentValue { get; set; }

    public string? SellerValue { get; set; }

    public bool IsMatch { get; set; }

    public string? Message { get; set; }
}
