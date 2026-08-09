namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// POST /api/admin/properties/{propertyId}/approve's request body - Admin
/// only, Phase B2 (Admin Property Moderation). The whole body is optional
/// and <see cref="Remarks"/> is nullable - mirrors
/// <c>usp_Admin_ApproveProperty</c>'s own optional <c>@Remarks</c>
/// parameter (<c>NVARCHAR(500) = NULL</c>) exactly; nothing here is
/// required that the stored procedure doesn't already treat as optional.
/// </summary>
public class ApprovePropertyRequest
{
    public string? Remarks { get; set; }
}
