using System.Text.RegularExpressions;
using LandGuard.Application.DTOs.Ocr;

namespace LandGuard.Application.Services;

/// <summary>
/// Placeholder field extraction over raw OCR text - simple label-based and
/// regex heuristics, exactly the level of sophistication Module 5B's
/// brief calls for ("simple regex or placeholder parsing is sufficient").
/// No AI, no trained model; this is pure, stateless, dependency-free C#
/// (no OCR/HTTP/DB access), so unlike every other piece of business logic
/// in this solution it doesn't need an interface/DI registration -
/// <c>OcrDocumentService</c> calls <see cref="Extract"/> directly, and so
/// does <c>GovernmentDeedComparisonService</c> (Phase 4 of the Government
/// Registry module) - for the government deed PDF's OCR text as well as
/// the seller's, the exact same extractor, no second implementation.
///
/// Deed layouts vary enough that most fields here are matched by scanning
/// for a label ("Owner", "District", ...) and taking the text that follows
/// it on the same line - a first-pass heuristic, not a parser, and one
/// this solution is expected to refine once real deed samples are
/// available. NIC and Date are exceptions: both have a recognizable
/// self-contained format, so they're matched directly against the whole
/// text rather than requiring a label.
///
/// The four fields below "Province" (PropertyReference, RegisteredPrice,
/// Status, plus the "Land Size" alias on LandExtent) were added
/// additively for the Government Registry module's deed-comparison
/// feature (Phase 4) - the original 10 fields/labels are unchanged, so
/// every existing caller of <c>POST /api/ocr/extract</c> keeps seeing
/// exactly the same output for documents it already handled correctly.
/// RegisteredPrice is intentionally still a raw string here (e.g. "LKR
/// 5,500,000") - stripping currency symbols/commas and parsing a decimal
/// is a comparison-specific concern, done by the caller that maps this
/// output into <c>GovernmentDeedData</c>/<c>SellerDeedData</c>, not by
/// this general-purpose text extractor.
/// </summary>
internal static class DocumentFieldExtractor
{
    // Mirrors AuthValidationRules.NicPattern's shape (old format: 9 digits
    // + V/X; new format: 12 digits) but unanchored, since this scans for a
    // NIC appearing anywhere inside free-form OCR text rather than
    // validating a whole input string.
    private static readonly Regex NicPattern = new(@"\b([0-9]{9}[VvXx]|[0-9]{12})\b", RegexOptions.Compiled);

    // dd/mm/yyyy, dd-mm-yyyy, dd.mm.yyyy, or yyyy-mm-dd - the common
    // numeric date formats a scanned deed's stamp/date line is likely to
    // use. Deliberately not attempting to parse "5th January 2024"-style
    // dates or validate the date is a real calendar date - best-effort
    // placeholder parsing only.
    private static readonly Regex DatePattern = new(
        @"\b(\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}|\d{4}-\d{1,2}-\d{1,2})\b", RegexOptions.Compiled);

    private static readonly (string FieldName, string[] Labels)[] LabeledFields =
    {
        ("OwnerName", new[] { "Owner's Name", "Owner Name", "Name of Owner", "Registered Owner", "Owner", "Grantee" }),
        ("PropertyAddress", new[] { "Property Address", "Situated at", "Address" }),
        ("ParcelNumber", new[] { "Parcel Number", "Parcel No", "Lot Number", "Lot No" }),
        ("RegistrationNumber", new[] { "Registration Number", "Registration No", "Reg No", "Deed Number", "Deed No" }),
        ("SurveyPlanNumber", new[] { "Survey Plan Number", "Survey Plan No", "Plan Number", "Plan No" }),
        // "Land Size" added (Phase 4) alongside the original three labels -
        // the Government Registry deed fixtures print "Land Size:", which
        // this field's original label set did not recognise.
        ("LandExtent", new[] { "Land Extent", "Extent", "Area", "Land Size" }),
        ("District", new[] { "District" }),
        ("Province", new[] { "Province" }),
        // ---- Added for Phase 4 (Government Registry deed comparison) ----
        ("PropertyReference", new[] { "Property Reference", "Property Ref", "Parcel Reference" }),
        ("RegisteredPrice", new[] { "Registered Price", "Purchase Price", "Consideration", "Price" }),
        ("Status", new[] { "Status" })
    };

    /// <summary>
    /// Runs every field's heuristic over <paramref name="rawText"/> and
    /// returns exactly 13 <see cref="ExtractedField"/> entries (the
    /// original 10 from Module 5B, plus PropertyReference, RegisteredPrice
    /// and Status added additively for Phase 4), in a fixed order, whether
    /// or not each was found - so a caller can always index/display the
    /// full set without checking for missing entries.
    /// </summary>
    public static IReadOnlyList<ExtractedField> Extract(string? rawText)
    {
        var text = rawText ?? string.Empty;

        var fields = new List<ExtractedField>(LabeledFields.Length + 2);

        foreach (var (fieldName, labels) in LabeledFields)
        {
            fields.Add(ExtractLabeled(text, fieldName, labels));
        }

        fields.Add(ExtractPattern(text, "NIC", NicPattern));
        fields.Add(ExtractPattern(text, "Date", DatePattern));

        return fields;
    }

    private static ExtractedField ExtractLabeled(string text, string fieldName, string[] labels)
    {
        foreach (var label in labels)
        {
            // "<label>[:-]  <rest of the line>" - the near-universal shape
            // of a labelled field on a form/deed, scanned line by line
            // since OCR output is naturally line-oriented.
            var match = Regex.Match(
                text,
                $@"{Regex.Escape(label)}\s*[:\-]\s*(?<value>[^\r\n]+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return new ExtractedField { FieldName = fieldName, Value = value, Found = true };
                }
            }
        }

        return new ExtractedField { FieldName = fieldName, Value = null, Found = false };
    }

    private static ExtractedField ExtractPattern(string text, string fieldName, Regex pattern)
    {
        var match = pattern.Match(text);

        return match.Success
            ? new ExtractedField { FieldName = fieldName, Value = match.Value, Found = true }
            : new ExtractedField { FieldName = fieldName, Value = null, Found = false };
    }
}
