namespace LandGuard.Application.DTOs.Admin;

/// <summary>
/// POST /api/admin/properties/{propertyId}/reject's request body - Admin
/// only, Phase B2 (Admin Property Moderation).
///
/// Unlike <c>ApprovePropertyRequest.Remarks</c>, <see cref="Reason"/> is
/// required at this API layer (see <c>RejectPropertyRequestValidator</c>)
/// even though the underlying <c>usp_Admin_RejectProperty</c>'s own
/// <c>@Remarks</c> parameter is technically optional (<c>NVARCHAR(500) =
/// NULL</c>, falling back to "Failed fraud verification." in the
/// seller's notification if omitted) - rejecting a listing without
/// telling the seller why is poor practice this API deliberately does
/// not allow, without changing what the stored procedure itself accepts
/// or requires.
/// </summary>
public class RejectPropertyRequest
{
    public string Reason { get; set; } = null!;
}
