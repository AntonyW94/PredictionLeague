using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One prize a player has won in the season, and what it was for.</summary>
/// <remarks>
/// The first name is not nullable here because the column is not: an account has one from the moment it finishes signing up, and
/// nobody without one has ever won a prize.
///
/// <see cref="RoundNumber"/> and <see cref="Month"/> are the prize's scope: a round prize has a round, a monthly prize has a
/// month, and a season-long prize has neither. That pattern is what the sent-log has to be matched on.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeWinningRow(
    string UserId,
    string Email,
    string FirstName,
    int LeagueId,
    string LeagueName,
    int LeaguePrizeSettingId,
    PrizeType PrizeType,
    string? PrizeDescription,
    int Rank,
    string? Stage,
    decimal Amount,
    int? RoundNumber,
    int? Month);
