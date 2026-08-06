using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Sharing.Models;

/// <summary>
/// A single match row on a prediction share card: the two teams, the player's predicted
/// scoreline and, once the match has been scored, the actual scoreline and how the pick did.
/// </summary>
/// <remarks>
/// <paramref name="HomeTeamAbbreviation"/> / <paramref name="AwayTeamAbbreviation"/> are the
/// three-letter fallbacks drawn in a coloured badge when a logo is missing or cannot be
/// decoded. <paramref name="IsScored"/> says whether <paramref name="ActualHomeScore"/> /
/// <paramref name="ActualAwayScore"/> should be shown and the pick colour-coded by
/// <paramref name="Outcome"/>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public record ShareCardMatch(
    string HomeTeamShortName,
    string HomeTeamAbbreviation,
    string? HomeTeamLogoUrl,
    string AwayTeamShortName,
    string AwayTeamAbbreviation,
    string? AwayTeamLogoUrl,
    int PredictedHomeScore,
    int PredictedAwayScore,
    bool IsScored,
    int? ActualHomeScore,
    int? ActualAwayScore,
    PredictionOutcome Outcome);
