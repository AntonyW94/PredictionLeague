using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Flat Dapper row for the prize-won aggregation: one row per won prize. User-level and round-level
/// fields repeat across a user's prize rows and are collapsed when grouped into
/// <see cref="PrizeWinner"/>.
/// </summary>
/// <remarks>
/// SELECT column order in <c>GetPrizeWinnersForRoundQueryHandler</c> must match this constructor
/// exactly (Dapper maps positionally by name and type).
/// </remarks>
public record PrizeWinnerRow(
    string UserId,
    string Email,
    string FirstName,
    string RoundName,
    int LeagueId,
    string LeagueName,
    int LeaguePrizeSettingId,
    PrizeType PrizeType,
    string? PrizeDescription,
    int Rank,
    string? Stage,
    decimal Amount,
    int? RoundNumber,
    int? Month,
    string? PrizeRoundName,
    bool AlreadyNotified);
