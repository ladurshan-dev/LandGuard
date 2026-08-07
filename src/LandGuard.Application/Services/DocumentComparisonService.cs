using FluentValidation;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.Fraud;
using LandGuard.Application.DTOs.Ocr;
using LandGuard.Domain.Enums;

namespace LandGuard.Application.Services;

/// <summary>
/// Orchestrates OCR-based deed comparison for Module 5C. Composes
/// <see cref="IPropertyStoredProcedures"/> (raw property, for the strict
/// ownership check - the same reasoning FraudDetectionService.
/// AnalyzePropertyAsync uses), <see cref="IPropertyService"/> (the
/// Approved-or-owner/Admin visibility rule for reads),
/// <see cref="IUserStoredProcedures"/> (the owning seller's profile - both
/// for the "seller is active" check and to read the seller's Name/NIC as
/// database values to compare against), <see cref="IFraudDetectionService"/>
/// (the existing Module 5A engine, read-only - see below) and the new
/// <see cref="IDocumentComparisonStoredProcedures"/>. No OCR runs here -
/// <see cref="DocumentComparisonRequest.Fields"/> is Module 5B's own
/// already-produced output, supplied by the caller.
///
/// <b>Field-to-database mapping.</b> Of the 10 fields Module 5B extracts,
/// LandGuardDB has a genuine, honest database counterpart for 7:
/// OwnerName -&gt; the seller's Name; NIC -&gt; the seller's NIC;
/// PropertyAddress -&gt; Property.Location; RegistrationNumber -&gt;
/// Property.DeedReference (the closest existing concept to a deed's
/// registration/reference number); LandExtent -&gt; Property.Size (perches,
/// compared numerically with tolerance); District -&gt; Property.District;
/// Province -&gt; derived from Property.District via a fixed, standard
/// Sri Lanka district-to-province mapping (DistrictToProvince below) -
/// LandGuardDB has no Province column of its own, but the mapping is fixed
/// public knowledge, not fabricated data. The remaining 3 have no
/// reasonable database counterpart under "no database redesign":
/// ParcelNumber and SurveyPlanNumber have no corresponding column at all,
/// and Date (the deed's registration date) is deliberately NOT compared
/// against Property.UploadDate - UploadDate is when the listing was
/// submitted to LandGuard, a different thing entirely, and treating it as
/// the deed's registration date would produce a false mismatch on every
/// single comparison (a deed registered years ago, listed today, is normal
/// - not fraud). These 3 fields are reported via
/// <see cref="FieldComparer.NotAvailable"/> with an honest explanation
/// rather than compared against unrelated data.
///
/// <b>Fraud Detection Foundation integration.</b> This module does not
/// modify usp_Fraud_AnalyseProperty/usp_Risk_GenerateReport, does not write
/// to dbo.FraudCheck/dbo.RiskReport, and does not build a second scoring
/// engine - comparison results carry no weight in the existing risk score.
/// Instead, every comparison response also carries the property's current
/// fraud risk, read (never recalculated) via
/// <see cref="IFraudDetectionService.CalculateRiskScoreAsync"/> - the
/// existing engine's own output, reused as-is, presented alongside the
/// document comparison so a caller sees one coherent picture without this
/// module editing a completed Module 5A file or duplicating its logic.
/// </summary>
public class DocumentComparisonService : IDocumentComparisonService
{
    private readonly IPropertyStoredProcedures _propertyStoredProcedures;
    private readonly IPropertyService _propertyService;
    private readonly IUserStoredProcedures _userStoredProcedures;
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly IDocumentComparisonStoredProcedures _comparisonStoredProcedures;
    private readonly IValidator<DocumentComparisonRequest> _validator;

    private static readonly string AdminRoleValue = UserRole.Administrator.ToDbValue();

    // Sri Lanka's 25 districts grouped into their 9 provinces - fixed,
    // standard public knowledge (not fraud-engine data, not fabricated),
    // used only to derive a Province value to compare against when
    // LandGuardDB itself has no Province column.
    private static readonly IReadOnlyDictionary<string, string> DistrictToProvince =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Colombo"] = "Western", ["Gampaha"] = "Western", ["Kalutara"] = "Western",
            ["Kandy"] = "Central", ["Matale"] = "Central", ["Nuwara Eliya"] = "Central",
            ["Galle"] = "Southern", ["Matara"] = "Southern", ["Hambantota"] = "Southern",
            ["Jaffna"] = "Northern", ["Kilinochchi"] = "Northern", ["Mannar"] = "Northern",
            ["Vavuniya"] = "Northern", ["Mullaitivu"] = "Northern",
            ["Batticaloa"] = "Eastern", ["Ampara"] = "Eastern", ["Trincomalee"] = "Eastern",
            ["Kurunegala"] = "North Western", ["Puttalam"] = "North Western",
            ["Anuradhapura"] = "North Central", ["Polonnaruwa"] = "North Central",
            ["Badulla"] = "Uva", ["Monaragala"] = "Uva",
            ["Ratnapura"] = "Sabaragamuwa", ["Kegalle"] = "Sabaragamuwa"
        };

    public DocumentComparisonService(
        IPropertyStoredProcedures propertyStoredProcedures,
        IPropertyService propertyService,
        IUserStoredProcedures userStoredProcedures,
        IFraudDetectionService fraudDetectionService,
        IDocumentComparisonStoredProcedures comparisonStoredProcedures,
        IValidator<DocumentComparisonRequest> validator)
    {
        _propertyStoredProcedures = propertyStoredProcedures;
        _propertyService = propertyService;
        _userStoredProcedures = userStoredProcedures;
        _fraudDetectionService = fraudDetectionService;
        _comparisonStoredProcedures = comparisonStoredProcedures;
        _validator = validator;
    }

    public async Task<Result<DocumentComparisonResponse>> CompareDocumentAsync(
        int propertyId, DocumentComparisonRequest request, int callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Property exists.
        var existing = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (existing is null)
        {
            return Result<DocumentComparisonResponse>.Failure("Property not found.");
        }

        // Property belongs to seller (or caller is an Admin) - the same
        // strict ownership check FraudDetectionService.AnalyzePropertyAsync
        // uses, not the "or Approved/public" visibility rule
        // GetLatestComparisonAsync uses below.
        var isOwner = existing.Listing.SellerId == callerId;
        if (!isOwner && !IsAdmin(callerRole))
        {
            return Result<DocumentComparisonResponse>.Failure("Only the property's owner or an administrator may run a document comparison.");
        }

        // Property (seller) is active - same definition as
        // FraudDetectionService (Users.IsActive of the owning seller).
        var seller = await _userStoredProcedures.GetByIdAsync(existing.Listing.SellerId, cancellationToken);
        if (seller is null || !seller.IsActive)
        {
            return Result<DocumentComparisonResponse>.Failure("The property's seller account is inactive; comparison is unavailable.");
        }

        var fieldRows = BuildFieldComparisons(request.Fields, existing.Listing, seller);
        var overallMatchPercentage = fieldRows.Count > 0
            ? Math.Round(fieldRows.Average(f => f.SimilarityPercentage), 2)
            : 0m;
        var fieldsMatched = fieldRows.Count(f => f.Matched);
        var summary = $"{fieldsMatched} of {fieldRows.Count} fields matched ({overallMatchPercentage:0.##}% average similarity).";

        var record = await _comparisonStoredProcedures.SaveAsync(
            propertyId, callerId, request.DocumentReference, overallMatchPercentage, summary, fieldRows, cancellationToken);

        var risk = await _fraudDetectionService.CalculateRiskScoreAsync(propertyId, callerId, callerRole, cancellationToken);

        return Result<DocumentComparisonResponse>.Success(BuildResponse(record, risk));
    }

    public async Task<Result<DocumentComparisonResponse>> GetLatestComparisonAsync(
        int propertyId, int? callerId, string? callerRole, CancellationToken cancellationToken = default)
    {
        // Same visibility rule as IFraudDetectionService.GetFraudReportAsync
        // - a Buyer may read once the property is Approved; the owner or an
        // Admin may always read.
        var visibility = await _propertyService.GetByIdAsync(propertyId, callerId, callerRole, cancellationToken);
        if (!visibility.Succeeded)
        {
            return Result<DocumentComparisonResponse>.Failure(visibility.Errors);
        }

        var record = await _comparisonStoredProcedures.GetLatestAsync(propertyId, cancellationToken);
        if (record is null)
        {
            return Result<DocumentComparisonResponse>.Failure("No document comparison has been run for this property yet.");
        }

        var risk = await _fraudDetectionService.CalculateRiskScoreAsync(propertyId, callerId, callerRole, cancellationToken);

        return Result<DocumentComparisonResponse>.Success(BuildResponse(record, risk));
    }

    private static DocumentComparisonResponse BuildResponse(DocumentComparisonRecord record, Result<RiskSummaryResponse> risk) => new()
    {
        PropertyId = record.Header.PropertyId,
        DocumentReference = record.Header.DocumentReference,
        Result = new ComparisonResultResponse
        {
            ComparisonId = record.Header.ComparisonId,
            FieldsCompared = record.Header.FieldsCompared,
            FieldsMatched = record.Header.FieldsMatched,
            OverallMatchPercentage = record.Header.OverallMatchPercentage,
            Summary = record.Header.Summary,
            ComparisonDate = record.Header.ComparisonDate,
            Fields = record.Fields.Select(f => new FieldComparisonResponse
            {
                FieldName = f.FieldName,
                OcrValue = f.OcrValue,
                DatabaseValue = f.DatabaseValue,
                Matched = f.Matched,
                SimilarityPercentage = f.SimilarityPercentage,
                Message = f.Message ?? string.Empty
            }).ToList()
        },
        CurrentFraudRisk = risk.Succeeded ? risk.Data : null
    };

    private static List<DocumentComparisonFieldRow> BuildFieldComparisons(
        IReadOnlyList<ExtractedField> ocrFields, PropertyListingResult listing, UserProfile seller)
    {
        // Last-one-wins on a duplicate FieldName - the request is
        // caller-supplied, not schema-enforced unique.
        var ocrLookup = ocrFields
            .Where(f => !string.IsNullOrWhiteSpace(f.FieldName))
            .GroupBy(f => f.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

        string? Ocr(string name) => ocrLookup.TryGetValue(name, out var value) ? value : null;

        return new List<DocumentComparisonFieldRow>
        {
            FieldComparer.CompareText("OwnerName", Ocr("OwnerName"), seller.Name),
            FieldComparer.CompareExact("NIC", Ocr("NIC"), seller.Nic),
            FieldComparer.CompareText("PropertyAddress", Ocr("PropertyAddress"), listing.Location),
            FieldComparer.NotAvailable("ParcelNumber", Ocr("ParcelNumber"), "LandGuardDB does not store a separate parcel number for a listing - not compared."),
            FieldComparer.CompareExact("RegistrationNumber", Ocr("RegistrationNumber"), listing.DeedReference),
            FieldComparer.NotAvailable("SurveyPlanNumber", Ocr("SurveyPlanNumber"), "LandGuardDB does not store a survey plan number for a listing - not compared."),
            FieldComparer.CompareNumeric("LandExtent", Ocr("LandExtent"), listing.Size, "perches"),
            FieldComparer.CompareExact("District", Ocr("District"), listing.District),
            FieldComparer.CompareExact("Province", Ocr("Province"), DeriveProvince(listing.District)),
            FieldComparer.NotAvailable("Date", Ocr("Date"), "Property.UploadDate reflects the listing's submission date, not the deed's registration date - not compared.")
        };
    }

    private static string? DeriveProvince(string? district) =>
        district is not null && DistrictToProvince.TryGetValue(district.Trim(), out var province) ? province : null;

    private static bool IsAdmin(string? callerRole) => string.Equals(callerRole, AdminRoleValue, StringComparison.Ordinal);
}
