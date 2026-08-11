using ThePredictions.Contracts.Admin.Rounds;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// One round, shaped for the administrator's screens. Shared by the list and the editor, which returned the same eight
/// fields from two statements.
/// </summary>
internal static class AdminRoundMapping
{
    public static RoundDto ToDto(AdminRoundRow round) =>
        new(round.Id,
            round.SeasonId,
            round.RoundNumber,
            round.ApiRoundName,
            round.StartDateUtc,
            round.DeadlineUtc,
            round.Status,
            round.MatchCount);
}
