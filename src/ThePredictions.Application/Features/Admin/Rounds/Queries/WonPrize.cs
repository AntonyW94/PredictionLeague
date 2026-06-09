using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// One prize a user has won, ready to render into the "Prize Won" email. <see cref="PrizeRoundName"/>
/// is the display name of the round the prize was won in (Round prizes only); otherwise null.
/// <see cref="AlreadyNotified"/> is true when the winner has previously been emailed about this exact
/// prize (matched against the <c>PrizeNotifications</c> sent-log).
/// </summary>
public record WonPrize(
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
