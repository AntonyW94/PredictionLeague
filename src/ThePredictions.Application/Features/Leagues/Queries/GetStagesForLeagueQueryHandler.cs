using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The tournament stages a league's leaderboard can be filtered by, in the order they are played.
/// </summary>
/// <remarks>
/// The sibling of <see cref="GetMonthsForLeagueQueryHandler"/>. Only rounds mapped to a stage count: a season with no
/// tournament structure offers no stages at all, which is what the old statement's inner join to
/// <c>TournamentRoundMappings</c> did.
/// </remarks>
public class GetStagesForLeagueQueryHandler(
    ILeagueSeasonRoundsQuery seasonRoundsQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetStagesForLeagueQuery, IEnumerable<StageDto>>
{
    public async Task<IEnumerable<StageDto>> Handle(GetStagesForLeagueQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var rounds = await seasonRoundsQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        var stages = rounds
            .Where(round => round.Stages is not null)
            .GroupBy(round => TournamentStageClassifier.ClassifyFrom(round.Stages))
            .Select(stage => new
            {
                Stage = stage.Key,
                FirstRoundNumber = stage.Min(round => round.RoundNumber),
                Progress = RoundProgress.Of(stage.Select(round => round.Status))
            })
            .Where(stage => stage.Progress.HasVisibleRound)
            .OrderBy(stage => stage.FirstRoundNumber)
            .ToList();

        return stages
            .Select(stage => new StageDto(
                stage.Stage,
                TournamentStageName.For(stage.Stage),
                stage.Progress.RoundsRemaining,
                stage.Progress.RoundsCompleted))
            .ToList();
    }
}
