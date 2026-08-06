using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Sharing.Models;

/// <summary>
/// Everything a <see cref="ThePredictions.Application.Services.IShareCardRenderer"/> needs to
/// draw a player's prediction share card for a single round.
/// </summary>
/// <remarks>
/// <paramref name="PlayerName"/> is the player's first name, or null when it is unknown - the
/// renderer titles the card "{PlayerName}'s Predictions", falling back to "My Predictions".
/// <paramref name="Theme"/> selects the light or dark colour scheme (and matching brand logo).
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public record ShareCardModel(
    string? PlayerName,
    string SeasonName,
    string RoundLabel,
    IReadOnlyList<ShareCardMatch> Matches,
    ShareCardTheme Theme);
