using LandGuard.Domain.Enums;

namespace LandGuard.Domain.Entities;

/// <summary>
/// Maps to <c>dbo.FraudCheck</c> - one row per analysis run of the 7
/// independent fraud rules. <b>Convention: <c>true</c> means the rule
/// FIRED</b> (a fraud indicator was detected); <c>false</c> means it
/// passed cleanly. A property can have several rows (seller corrects and
/// resubmits); <c>vw_PropertyLatestRisk</c> / <see cref="ReadModels.PropertyLatestRisk"/>
/// exposes the current one. Written exclusively by
/// <c>usp_Fraud_AnalyseProperty</c>.
/// </summary>
public class FraudCheck
{
    public int FraudCheckId { get; set; }

    public int PropertyId { get; set; }

    /// <summary>Rule 1 - price per perch more than the configured threshold below the district benchmark.</summary>
    public bool PriceCheck { get; set; }

    /// <summary>Rule 2 - an image hash on this listing already exists on another property.</summary>
    public bool DuplicateCheck { get; set; }

    /// <summary>Rule 3 - seller NIC missing, malformed, unverified, inactive, or shared with another account.</summary>
    public bool NicCheck { get; set; }

    /// <summary>Rule 4 - same deed reference already used by another live listing.</summary>
    public bool DeedCheck { get; set; }

    /// <summary>Rule 5 - seller already has 2+ rejected listings or resolved suspicious reports.</summary>
    public bool SellerHistoryCheck { get; set; }

    /// <summary>Rule 6 - coordinates missing or outside the Sri Lankan bounding box.</summary>
    public bool LocationCheck { get; set; }

    /// <summary>Rule 7 - a mandatory listing detail (deed, description, images, district, phone) is absent.</summary>
    public bool MissingInfoCheck { get; set; }

    public FraudStatus FraudStatus { get; set; }

    public DateTime CheckDate { get; set; }

    // Navigation properties -------------------------------------------------

    public Property Property { get; set; } = null!;

    /// <summary>Point 8 of the engine - the combined score. 1:1, may be null momentarily between insert and usp_Risk_GenerateReport.</summary>
    public RiskReport? RiskReport { get; set; }
}
