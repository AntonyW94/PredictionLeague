namespace ThePredictions.Contracts.Prizes;

/// <summary>
/// An admin's prize-scheme configuration: the admin top-up, the £5-rounding threshold, and the
/// per-entry allocation across enabled categories. Submitted at league creation or once via Edit.
/// </summary>
public class PrizeSchemeRequest
{
    public int AdminTopUpPounds { get; set; }
    public int OverallFivePoundThreshold { get; set; } = 100;
    public List<PrizeSchemeCategoryRequest> Categories { get; set; } = [];
}
