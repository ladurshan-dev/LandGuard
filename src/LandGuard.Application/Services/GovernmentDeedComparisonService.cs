using System.Globalization;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;
using LandGuard.Application.DTOs.GovernmentRegistry;
using LandGuard.Application.DTOs.Ocr;
using LandGuard.Domain.Enums;
using LandGuard.Domain.Exceptions;

namespace LandGuard.Application.Services;

/// <summary>
/// Orchestrates the Government Registry module's Phase 4 deed comparison.
/// No SQL, no HTTP, no OCR engine, no file I/O of its own - it composes
/// <see cref="IPropertyStoredProcedures"/> (ownership check),
/// <see cref="IOcrDocumentService"/> (the existing seller document
/// storage + OCR + field-extraction pipeline, reused wholesale),
/// <see cref="IGovernmentRegistryService"/> (the existing trusted-record
/// lookup), <see cref="IFileStorageService"/> (the new
/// <c>OpenDocumentAsync</c> read method, Phase 4) and
/// <see cref="IOcrService"/> (the same Tesseract engine, applied directly
/// to the government PDF rather than through
/// <see cref="IOcrDocumentService"/>, since that document is already
/// stored and must not be re-saved as if it were a fresh seller upload).
/// <see cref="DocumentFieldExtractor"/> and <see cref="DeedFieldComparer"/>
/// are called directly, the same "no interface needed for pure C#" shape
/// they already establish.
///
/// <see cref="SellerDeedData"/> is built exclusively from OCR'ing the
/// seller's actually-uploaded file (plus <c>Property.Price</c>, already
/// legitimately captured) - nothing here ever accepts or trusts a
/// client-supplied field value claiming to be "the contents of the deed".
/// </summary>
public class GovernmentDeedComparisonService : IGovernmentDeedComparisonService
{
    private readonly IPropertyStoredProcedures _propertyStoredProcedures;
    private readonly IOcrDocumentService _ocrDocumentService;
    private readonly IGovernmentRegistryService _governmentRegistryService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOcrService _ocrService;

    private static readonly string AdminRoleValue = UserRole.Administrator.ToDbValue();

    public GovernmentDeedComparisonService(
        IPropertyStoredProcedures propertyStoredProcedures,
        IOcrDocumentService ocrDocumentService,
        IGovernmentRegistryService governmentRegistryService,
        IFileStorageService fileStorageService,
        IOcrService ocrService)
    {
        _propertyStoredProcedures = propertyStoredProcedures;
        _ocrDocumentService = ocrDocumentService;
        _governmentRegistryService = governmentRegistryService;
        _fileStorageService = fileStorageService;
        _ocrService = ocrService;
    }

    public async Task<Result<GovernmentDeedComparisonReport>> CompareAsync(
        int propertyId,
        string fileName,
        string contentType,
        Stream sellerDeedContent,
        int callerId,
        string? callerRole,
        CancellationToken cancellationToken = default)
    {
        // Strict ownership check against the raw property (not
        // IPropertyService.GetByIdAsync's "or Approved/public" visibility
        // rule) - the same distinction FraudDetectionService.AnalyzePropertyAsync
        // draws for the same reason: triggering an action on a property is
        // not the same authorization question as reading an already-public
        // listing, and reusing the public-visibility rule here would wrongly
        // let any Seller run a comparison against another Seller's already-
        // Approved listing.
        var property = await _propertyStoredProcedures.GetByIdAsync(propertyId, cancellationToken);
        if (property is null)
        {
            throw new NotFoundException("Property not found.");
        }

        if (property.Listing.SellerId != callerId && !IsAdmin(callerRole))
        {
            throw new UnauthorizedAccessException();
        }

        // Seller side: the EXISTING save + OCR + field-extraction pipeline,
        // reused as-is - uploadedByUserId is the caller's own real id
        // (Seller or Admin), never a fake/borrowed one. If the upload is
        // empty, oversized, or an unsupported type, IOcrDocumentService's
        // own validation already produces the right failure message.
        var ocrResult = await _ocrDocumentService.ExtractAsync(fileName, contentType, sellerDeedContent, callerId, cancellationToken);
        if (!ocrResult.Succeeded)
        {
            return Result<GovernmentDeedComparisonReport>.Failure(ocrResult.Errors);
        }

        var sellerDeed = MapToSellerDeedData(ocrResult.Data!.Fields, property.Listing.Price);

        // The seller's own document is already saved by this point
        // (IOcrDocumentService.ExtractAsync's SaveDocumentAsync call, above)
        // regardless of what the government-record lookup below finds -
        // captured once here so every return path (Scenario F included)
        // can carry it through to persistence.
        var sellerDocumentReference = ocrResult.Data!.DocumentReference;

        var governmentRecord = await ResolveGovernmentRecordAsync(property.Listing.DeedReference, sellerDeed, cancellationToken);

        // Scenario F (missing/cancelled): resolved via
        // IGovernmentRegistryService only - DummyGovernmentRegistryService
        // is never referenced directly, so a real government API
        // implementation drops in later with zero change here. No
        // government PDF OCR is attempted in this branch, per the explicit
        // "without attempting unnecessary government PDF OCR" instruction.
        if (governmentRecord is null
            || !string.Equals(governmentRecord.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(governmentRecord.DeedDocumentReference))
        {
            return Result<GovernmentDeedComparisonReport>.Success(
                BuildMissingOrCancelledReport(propertyId, governmentRecord, sellerDocumentReference));
        }

        await using var governmentStream = await _fileStorageService.OpenDocumentAsync(governmentRecord.DeedDocumentReference, cancellationToken);
        if (governmentStream is null)
        {
            // The record exists and is Active, but its PDF isn't actually
            // readable on disk (missing/moved/corrupted reference) - same
            // "unavailable" outcome as a genuinely missing record, per
            // IFileStorageService.OpenDocumentAsync's own doc comment.
            return Result<GovernmentDeedComparisonReport>.Success(
                BuildMissingOrCancelledReport(propertyId, governmentRecord, sellerDocumentReference));
        }

        var governmentContentType = InferContentType(governmentRecord.DeedDocumentReference);
        var governmentOcr = await _ocrService.ExtractTextAsync(governmentStream, governmentContentType, cancellationToken);
        var governmentFields = DocumentFieldExtractor.Extract(governmentOcr.RawText);
        var governmentDeed = MapToGovernmentDeedData(governmentFields, governmentRecord.Status);

        var fieldResults = DeedFieldComparer.Compare(governmentDeed, sellerDeed);
        var overallOutcome = fieldResults.All(f => f.Match) ? "Clean" : "Mismatch";

        var report = new GovernmentDeedComparisonReport
        {
            PropertyId = propertyId,
            GovernmentRecordId = governmentRecord.RecordId,
            GovernmentRecordFound = true,
            GovernmentRecordStatus = governmentRecord.Status,
            OverallOutcome = overallOutcome,
            Fields = fieldResults,
            GeneratedDate = DateTime.UtcNow,
            SellerDocumentReference = sellerDocumentReference
        };

        return Result<GovernmentDeedComparisonReport>.Success(report);
    }

    /// <summary>
    /// Tries, in order: the property's own already-captured
    /// <c>DeedReference</c> (more reliable than anything OCR just read off
    /// a scanned image, and already legitimately captured by LandGuard -
    /// tried first for exactly that reason), then the seller deed's OCR'd
    /// deed number, then its OCR'd NIC, then its OCR'd property reference.
    /// Returns the first match; null if none of the four resolve to a
    /// government record at all (part of Scenario F).
    /// </summary>
    private async Task<GovernmentLandRecordDto?> ResolveGovernmentRecordAsync(
        string? propertyDeedReference, SellerDeedData sellerDeed, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(propertyDeedReference))
        {
            var byPropertyDeedReference = await _governmentRegistryService.GetByDeedNumberAsync(propertyDeedReference, cancellationToken);
            if (byPropertyDeedReference is not null)
            {
                return byPropertyDeedReference;
            }
        }

        if (!string.IsNullOrWhiteSpace(sellerDeed.DeedNumber))
        {
            var byOcrDeedNumber = await _governmentRegistryService.GetByDeedNumberAsync(sellerDeed.DeedNumber, cancellationToken);
            if (byOcrDeedNumber is not null)
            {
                return byOcrDeedNumber;
            }
        }

        if (!string.IsNullOrWhiteSpace(sellerDeed.Nic))
        {
            var byNic = await _governmentRegistryService.GetByNicAsync(sellerDeed.Nic, cancellationToken);
            if (byNic is not null)
            {
                return byNic;
            }
        }

        if (!string.IsNullOrWhiteSpace(sellerDeed.PropertyReference))
        {
            return await _governmentRegistryService.GetByPropertyReferenceAsync(sellerDeed.PropertyReference, cancellationToken);
        }

        return null;
    }

    private static GovernmentDeedComparisonReport BuildMissingOrCancelledReport(
        int propertyId, GovernmentLandRecordDto? record, string? sellerDocumentReference) => new()
    {
        PropertyId = propertyId,
        GovernmentRecordId = record?.RecordId,
        GovernmentRecordFound = record is not null,
        GovernmentRecordStatus = record?.Status,
        OverallOutcome = "MissingOrCancelledGovernmentRecord",
        Fields = Array.Empty<DeedFieldComparisonResult>(),
        GeneratedDate = DateTime.UtcNow,
        SellerDocumentReference = sellerDocumentReference
    };

    private static SellerDeedData MapToSellerDeedData(IReadOnlyList<ExtractedField> fields, decimal propertyAskingPrice)
    {
        string? Value(string fieldName) => fields.FirstOrDefault(f => f.FieldName == fieldName)?.Value;

        return new SellerDeedData
        {
            Nic = Value("NIC"),
            OwnerName = Value("OwnerName"),
            DeedNumber = Value("RegistrationNumber"),
            PropertyReference = Value("PropertyReference"),
            LandSize = ParseDouble(Value("LandExtent")),
            District = Value("District"),
            Address = Value("PropertyAddress"),
            // Deliberately NOT read from the OCR'd deed text - see
            // SellerDeedData.AskingPrice's doc comment.
            AskingPrice = propertyAskingPrice,
            RegistrationDate = ParseDate(Value("Date"))
        };
    }

    private static GovernmentDeedData MapToGovernmentDeedData(IReadOnlyList<ExtractedField> fields, string governmentRecordStatus)
    {
        string? Value(string fieldName) => fields.FirstOrDefault(f => f.FieldName == fieldName)?.Value;

        return new GovernmentDeedData
        {
            Nic = Value("NIC"),
            OwnerName = Value("OwnerName"),
            DeedNumber = Value("RegistrationNumber"),
            PropertyReference = Value("PropertyReference"),
            LandSize = ParseDouble(Value("LandExtent")),
            District = Value("District"),
            Address = Value("PropertyAddress"),
            RegisteredPrice = ParseDecimal(Value("RegisteredPrice")),
            RegistrationDate = ParseDate(Value("Date")),
            // Read directly from GovernmentLandRecordDto.Status, not OCR -
            // see GovernmentDeedData.Status's doc comment.
            Status = governmentRecordStatus
        };
    }

    private static double? ParseDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var numericOnly = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());

        return double.TryParse(numericOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var numericOnly = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());

        return decimal.TryParse(numericOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Mirrors DocumentFieldExtractor.DatePattern's own formats.
        string[] formats = { "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "yyyy-M-d", "d/M/yyyy", "d-M-yyyy" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback) ? fallback : null;
    }

    private static string InferContentType(string storageReference)
    {
        var extension = Path.GetExtension(storageReference).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/pdf"
        };
    }

    private static bool IsAdmin(string? callerRole) => string.Equals(callerRole, AdminRoleValue, StringComparison.Ordinal);
}
