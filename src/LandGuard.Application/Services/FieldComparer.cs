using System.Globalization;
using System.Text.RegularExpressions;
using LandGuard.Application.Common.Models;

namespace LandGuard.Application.Services;

/// <summary>
/// Pure, stateless comparison logic for Module 5C - no AI, no external
/// service, exactly the "simple similarity algorithm... that can later be
/// improved" the brief calls for. Like <see cref="DocumentFieldExtractor"/>
/// this is dependency-free C# (no DB/HTTP access) so it needs no DI
/// registration; <c>DocumentComparisonService</c> calls its static methods
/// directly.
///
/// Every comparison is normalized first (trim, collapse internal
/// whitespace, uppercase) so "Colombo 07" and "colombo   07" are treated as
/// equal - the brief's "case-insensitive comparison, whitespace
/// normalization" requirement. Beyond that, three comparison styles are
/// used depending on the field:
/// - <see cref="CompareExact"/>: structured values (NIC, District,
///   Province, the deed reference/registration number) where Matched is a
///   strict normalized-equality check - "exact comparison where
///   appropriate".
/// - <see cref="CompareText"/>: free-text values (Owner Name, Address)
///   where near-misses (OCR noise, minor spelling differences) are still
///   useful signal - Matched is a threshold over a Levenshtein-distance
///   similarity percentage instead of exact equality.
/// - <see cref="CompareNumeric"/>: Land Extent, where the OCR text and the
///   database both represent the same quantity in different formatting
///   ("10.5 Perches" vs. a raw double) - compared with a tolerance rather
///   than string equality.
/// - <see cref="NotAvailable"/>: fields LandGuardDB's schema has no
///   dedicated column for (see DocumentComparisonService's field-mapping
///   doc comment) - reported honestly as "not compared" rather than
///   silently compared against the wrong data.
/// </summary>
internal static class FieldComparer
{
    private const decimal TextMatchThresholdPercent = 80m;
    private const double NumericTolerancePercent = 5.0;

    public static DocumentComparisonFieldRow CompareExact(string fieldName, string? ocrValue, string? dbValue)
    {
        if (string.IsNullOrWhiteSpace(ocrValue) || string.IsNullOrWhiteSpace(dbValue))
        {
            return NotEnoughData(fieldName, ocrValue, dbValue);
        }

        var normalizedOcr = Normalize(ocrValue);
        var normalizedDb = Normalize(dbValue);
        var matched = string.Equals(normalizedOcr, normalizedDb, StringComparison.Ordinal);
        var similarity = matched ? 100m : SimilarityPercentage(normalizedOcr, normalizedDb);

        return new DocumentComparisonFieldRow
        {
            FieldName = fieldName,
            OcrValue = ocrValue,
            DatabaseValue = dbValue,
            Matched = matched,
            SimilarityPercentage = similarity,
            Message = matched
                ? "Exact match after case/whitespace normalization."
                : $"Values differ ({similarity:0.##}% similar)."
        };
    }

    public static DocumentComparisonFieldRow CompareText(string fieldName, string? ocrValue, string? dbValue)
    {
        if (string.IsNullOrWhiteSpace(ocrValue) || string.IsNullOrWhiteSpace(dbValue))
        {
            return NotEnoughData(fieldName, ocrValue, dbValue);
        }

        var similarity = SimilarityPercentage(Normalize(ocrValue), Normalize(dbValue));
        var matched = similarity >= TextMatchThresholdPercent;

        return new DocumentComparisonFieldRow
        {
            FieldName = fieldName,
            OcrValue = ocrValue,
            DatabaseValue = dbValue,
            Matched = matched,
            SimilarityPercentage = similarity,
            Message = matched
                ? $"Considered a match ({similarity:0.##}% similar, threshold {TextMatchThresholdPercent}%)."
                : $"Below the {TextMatchThresholdPercent}% similarity threshold ({similarity:0.##}% similar)."
        };
    }

    public static DocumentComparisonFieldRow CompareNumeric(string fieldName, string? ocrValue, double dbValue, string unit)
    {
        var formattedDbValue = $"{dbValue.ToString("0.##", CultureInfo.InvariantCulture)} {unit}";

        if (string.IsNullOrWhiteSpace(ocrValue))
        {
            return NotEnoughData(fieldName, ocrValue, formattedDbValue);
        }

        var match = Regex.Match(ocrValue, @"\d+(\.\d+)?");
        if (!match.Success || !double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ocrNumber))
        {
            return new DocumentComparisonFieldRow
            {
                FieldName = fieldName,
                OcrValue = ocrValue,
                DatabaseValue = formattedDbValue,
                Matched = false,
                SimilarityPercentage = 0,
                Message = "Could not parse a numeric value from the OCR text."
            };
        }

        var percentDifference = dbValue == 0 ? 100 : Math.Abs(ocrNumber - dbValue) / dbValue * 100;
        var similarity = (decimal)Math.Max(0, 100 - percentDifference);
        var matched = percentDifference <= NumericTolerancePercent;

        return new DocumentComparisonFieldRow
        {
            FieldName = fieldName,
            OcrValue = ocrValue,
            DatabaseValue = formattedDbValue,
            Matched = matched,
            SimilarityPercentage = Math.Round(similarity, 2),
            Message = matched
                ? $"Within {NumericTolerancePercent}% tolerance."
                : $"Differs by {percentDifference:0.##}%, outside the {NumericTolerancePercent}% tolerance."
        };
    }

    /// <summary>For fields LandGuardDB's schema has no dedicated column for - honestly reported as not compared, rather than compared against an unrelated value.</summary>
    public static DocumentComparisonFieldRow NotAvailable(string fieldName, string? ocrValue, string reason) => new()
    {
        FieldName = fieldName,
        OcrValue = ocrValue,
        DatabaseValue = null,
        Matched = false,
        SimilarityPercentage = 0,
        Message = reason
    };

    private static DocumentComparisonFieldRow NotEnoughData(string fieldName, string? ocrValue, string? dbValue) => new()
    {
        FieldName = fieldName,
        OcrValue = ocrValue,
        DatabaseValue = dbValue,
        Matched = false,
        SimilarityPercentage = 0,
        Message = string.IsNullOrWhiteSpace(ocrValue)
            ? "Not present in the supplied OCR data."
            : "No corresponding database value to compare against."
    };

    private static string Normalize(string value) => Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

    // Simple Levenshtein-distance-based similarity percentage - the
    // placeholder "can later be improved" algorithm the brief calls for.
    // No external library, no AI/ML.
    private static decimal SimilarityPercentage(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 100m;
        if (a.Length == 0 || b.Length == 0) return 0m;

        var distance = LevenshteinDistance(a, b);
        var maxLength = Math.Max(a.Length, b.Length);
        var similarity = (1 - (double)distance / maxLength) * 100;

        return (decimal)Math.Round(Math.Max(0, similarity), 2);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            costs[0] = i;
            var previousDiagonal = i - 1;

            for (var j = 1; j <= b.Length; j++)
            {
                var previousDiagonalSave = costs[j];
                costs[j] = a[i - 1] == b[j - 1]
                    ? previousDiagonal
                    : 1 + Math.Min(previousDiagonal, Math.Min(costs[j], costs[j - 1]));
                previousDiagonal = previousDiagonalSave;
            }
        }

        return costs[b.Length];
    }
}
