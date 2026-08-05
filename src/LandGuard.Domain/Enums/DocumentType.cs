namespace LandGuard.Domain.Enums;

/// <summary>
/// Classifies an uploaded property document for the future OCR pipeline.
///
/// Module 2 note: LandGuardDB (the authoritative schema uploaded in
/// Module 2) has no PropertyDocument/OCR-result table - a deed is
/// represented only as <c>dbo.Property.DeedReference</c> (a plain
/// VARCHAR), and NIC verification is a single BIT flag
/// (<c>dbo.Users.NICVerified</c>) rather than a stored document. This
/// enum is therefore currently unused by any entity. If the OCR module
/// later needs to persist raw OCR output (extracted text, confidence
/// scores, the source image), that would require a new table and, per
/// this project's rules, that schema addition would be proposed to and
/// confirmed by the database owner before being added - not assumed here.
/// </summary>
public enum DocumentType
{
    DeedDocument = 1,
    SurveyPlan = 2,
    NicDocument = 3,
    Other = 4
}
