using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Application.Services;

/// <summary>
/// Stateless field-by-field diff between a <see cref="GovernmentDeedData"/>
/// (trusted) and a <see cref="SellerDeedData"/> (the seller's own
/// OCR'd/declared submission) - Government Registry module, Phase 4. Pure
/// C#, no OCR/HTTP/DB access, exactly the same "no interface/DI needed"
/// shape <c>DocumentFieldExtractor</c> already established in this
/// solution; <c>GovernmentDeedComparisonService</c> calls
/// <see cref="Compare"/> directly.
///
/// Text fields (NIC, Owner Name, Deed Number, Property Reference,
/// District, Address) are compared after trimming and collapsing internal
/// whitespace, case-insensitively - OCR output varies in whitespace and
/// case in ways that shouldn't count as a real mismatch. Land Size uses a
/// numeric tolerance, not exact/string equality (perches read off a
/// scanned document are rarely bit-for-bit identical even when correct).
/// Registered Price is handled separately from every other field: see
/// <see cref="ComparePrice"/>.
///
/// A field with no value on either side (typically: this OCR heuristic
/// didn't find a match in the source text - see DocumentFieldExtractor's
/// own reliability caveats) is reported as Match = true with an
/// "insufficient data" message rather than a mismatch - an extraction
/// miss is not evidence of fraud, and treating it as a hard failure would
/// make every result a false positive whenever OCR simply couldn't read a
/// field clearly.
/// </summary>
internal static class DeedFieldComparer
{
    /// <summary>
    /// Perches. A placeholder default, deliberately explicit and named
    /// (not a bare magic number) so it's easy to find and retune later -
    /// the same spirit as <c>dbo.FraudRuleWeight.Threshold</c> configuring
    /// the SQL engine's own price-anomaly margin. Scanned land-size text
    /// ("25.5 perches" vs "25.50 P.") can differ trivially without being a
    /// real mismatch; anything beyond this is treated as one.
    /// </summary>
    private const double LandSizeToleranceInPerches = 1.0;

    /// <summary>
    /// A deliberately generous threshold (50%) for the asking-price-vs-
    /// registered-price anomaly check - see <see cref="ComparePrice"/> for
    /// why these two numbers are expected to differ even for a completely
    /// genuine listing, and are therefore never compared for near-equality
    /// the way every other field is.
    /// </summary>
    private const decimal PriceAnomalyThreshold = 0.50m;

    /// <summary>Runs every comparison and returns one result per field, in a fixed order.</summary>
    public static IReadOnlyList<DeedFieldComparisonResult> Compare(GovernmentDeedData government, SellerDeedData seller) =>
        new List<DeedFieldComparisonResult>
        {
            CompareText("NIC", government.Nic, seller.Nic),
            CompareText("OwnerName", government.OwnerName, seller.OwnerName),
            CompareText("DeedNumber", government.DeedNumber, seller.DeedNumber),
            CompareText("PropertyReference", government.PropertyReference, seller.PropertyReference),
            CompareLandSize(government.LandSize, seller.LandSize),
            CompareText("District", government.District, seller.District),
            CompareText("Address", government.Address, seller.Address),
            ComparePrice(government.RegisteredPrice, seller.AskingPrice),
            CompareDate(government.RegistrationDate, seller.RegistrationDate)
        };

    private static DeedFieldComparisonResult CompareText(string fieldName, string? governmentValue, string? sellerValue)
    {
        var normalizedGovernment = Normalize(governmentValue);
        var normalizedSeller = Normalize(sellerValue);

        if (normalizedGovernment is null || normalizedSeller is null)
        {
            return Result(fieldName, governmentValue, sellerValue, true,
                "Could not be compared - the value was not found by OCR on one or both sides.");
        }

        var match = string.Equals(normalizedGovernment, normalizedSeller, StringComparison.OrdinalIgnoreCase);

        return Result(fieldName, governmentValue, sellerValue, match,
            match ? "Matches the trusted government record." : $"Does not match the trusted government record ({fieldName} mismatch).");
    }

    private static DeedFieldComparisonResult CompareLandSize(double? governmentValue, double? sellerValue)
    {
        if (governmentValue is null || sellerValue is null)
        {
            return Result("LandSize", FormatSize(governmentValue), FormatSize(sellerValue), true,
                "Could not be compared - the value was not found by OCR on one or both sides.");
        }

        var difference = Math.Abs(governmentValue.Value - sellerValue.Value);
        var match = difference <= LandSizeToleranceInPerches;

        return Result("LandSize", FormatSize(governmentValue), FormatSize(sellerValue), match,
            match
                ? $"Within the {LandSizeToleranceInPerches:0.##}-perch tolerance of the trusted government record."
                : $"Differs from the trusted government record by {difference:0.##} perches, beyond the {LandSizeToleranceInPerches:0.##}-perch tolerance.");
    }

    /// <summary>
    /// Government's RegisteredPrice and the seller's AskingPrice are
    /// different business concepts, not two readings of the same number
    /// (see SellerDeedData.AskingPrice's doc comment): RegisteredPrice is
    /// what the property was worth when a past registration/transfer was
    /// recorded, AskingPrice is what the seller is asking for it today, on
    /// this listing. A legitimate, non-fraudulent listing can easily show
    /// a large difference between the two - years of appreciation, a
    /// renovation, a change in the local market - none of which is
    /// suspicious by itself. This check only ever flags a GROSS anomaly
    /// (currently beyond a 50% deviation, see PriceAnomalyThreshold), and
    /// its message always states the distinction explicitly rather than
    /// implying the two numbers should have matched.
    /// </summary>
    private static DeedFieldComparisonResult ComparePrice(decimal? governmentRegisteredPrice, decimal? sellerAskingPrice)
    {
        const string distinction =
            "The government's registered/transfer price and the seller's current asking price are different business concepts and are not expected to be equal.";

        if (governmentRegisteredPrice is null || governmentRegisteredPrice.Value == 0 || sellerAskingPrice is null)
        {
            return Result("Price", FormatPrice(governmentRegisteredPrice), FormatPrice(sellerAskingPrice), true,
                $"Could not be evaluated - one of the two prices is unavailable. {distinction}");
        }

        var deviation = Math.Abs(sellerAskingPrice.Value - governmentRegisteredPrice.Value) / governmentRegisteredPrice.Value;
        var isAnomalous = deviation > PriceAnomalyThreshold;

        return Result("Price", FormatPrice(governmentRegisteredPrice), FormatPrice(sellerAskingPrice), !isAnomalous,
            isAnomalous
                ? $"The asking price deviates from the government's registered price by {deviation:P0}, beyond the {PriceAnomalyThreshold:P0} anomaly threshold. {distinction}"
                : $"Within the {PriceAnomalyThreshold:P0} anomaly threshold of the government's registered price. {distinction}");
    }

    private static DeedFieldComparisonResult CompareDate(DateTime? governmentValue, DateTime? sellerValue)
    {
        var governmentText = governmentValue?.ToString("yyyy-MM-dd");
        var sellerText = sellerValue?.ToString("yyyy-MM-dd");

        if (governmentValue is null || sellerValue is null)
        {
            return Result("RegistrationDate", governmentText, sellerText, true,
                "Could not be compared - the value was not found by OCR on one or both sides.");
        }

        var match = governmentValue.Value.Date == sellerValue.Value.Date;

        return Result("RegistrationDate", governmentText, sellerText, match,
            match
                ? "Matches the trusted government record."
                : "Does not match the trusted government record's registration date.");
    }

    private static DeedFieldComparisonResult Result(
        string fieldName, string? governmentValue, string? sellerValue, bool match, string message) => new()
    {
        FieldName = fieldName,
        GovernmentValue = governmentValue,
        SellerValue = sellerValue,
        Match = match,
        Message = message
    };

    /// <summary>Trims and collapses internal whitespace runs to a single space; null/whitespace-only becomes null so callers can treat "not found" uniformly.</summary>
    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? FormatSize(double? value) => value?.ToString("0.##") + (value is null ? null : " perches");

    private static string? FormatPrice(decimal? value) => value?.ToString("N0");
}
