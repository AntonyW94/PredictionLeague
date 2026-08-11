namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

/// <summary>
/// Which stored pricing row counts as the live one.
/// </summary>
/// <remarks>
/// The earliest, by id. This is a single-row table by convention rather than by constraint, so what to do if a second row
/// ever appears is a decision - and it was <c>TOP 1 ORDER BY [Id]</c> in two separate statements, the administrator's screen
/// and the price recommendation, with nothing tying them together.
/// </remarks>
internal static class LivePricingSettings
{
    public static PricingSettingsRow? From(IEnumerable<PricingSettingsRow> rows) => rows.MinBy(row => row.Id);
}
