namespace LandGuard.Application.Common.Models;

/// <summary>
/// One past verification run for a property, its field evidence and its
/// reasons grouped together - built by
/// <c>GovernmentDeedVerificationStoredProcedures.GetHistoryAsync</c> from
/// <c>usp_DeedVerification_GetHistory</c>'s 3 result sets, the same "read N
/// result sets off one GridReader, then group children under their parent"
/// composition <c>PropertyStoredProcedures.GetByIdAsync</c> already
/// establishes for <c>PropertyDetail</c>.
/// </summary>
public class DeedVerificationHistoryEntry
{
    public DeedVerificationRecord Record { get; set; } = null!;

    public IReadOnlyList<DeedVerificationFieldRecord> Fields { get; set; } = Array.Empty<DeedVerificationFieldRecord>();

    public IReadOnlyList<DeedVerificationReasonRecord> Reasons { get; set; } = Array.Empty<DeedVerificationReasonRecord>();
}
