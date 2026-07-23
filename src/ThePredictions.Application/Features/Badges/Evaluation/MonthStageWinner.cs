namespace ThePredictions.Application.Features.Badges.Evaluation;

/// <summary>
/// A month or stage a user won in a league. Keyed by the period's final round (so the badge is
/// repeatable per month/stage via the unique index) and dated to that round. Detail names the period.
/// </summary>
public record MonthStageWinner(string UserId, int LeagueId, int RoundId, DateTime AwardedUtc, string Detail);
