namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

/// <summary>Reads the stored pricing settings, or nothing when none have been saved.</summary>
/// <remarks>
/// What an absent row means - the built-in defaults - is a rule and stays with the handler, which is where it already
/// was. The read's own rule was <c>TOP 1 ORDER BY [Id]</c>: this is a single-row table by convention rather than by
/// constraint, so "the earliest row is the live one" is a decision, and it is now made in C#.
/// </remarks>
public interface IPricingSettingsQuery
{
    Task<IReadOnlyList<PricingSettingsRow>> ExecuteAsync(CancellationToken cancellationToken);
}
