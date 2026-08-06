using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetStagesForLeagueQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetStagesForLeagueQuery, IEnumerable<StageDto>>
{
    public async Task<IEnumerable<StageDto>> Handle(GetStagesForLeagueQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        const string sql = @"
            WITH StageAggregates AS (
                SELECT
                    CASE WHEN trm.[Stages] LIKE '%Group%' THEN @GroupStage ELSE @KnockoutStage END AS [Stage],

                    MIN(r.[RoundNumber]) AS [FirstRoundNumber],

                    SUM(CASE
                        WHEN r.[Status] <> @CompletedStatus THEN 1
                        ELSE 0
                    END) AS [RoundsRemaining],

                    SUM(CASE
                        WHEN r.[Status] = @CompletedStatus THEN 1
                        ELSE 0
                    END) AS [RoundsCompleted],

                    SUM(CASE
                        WHEN r.[Status] <> @DraftStatus THEN 1
                        ELSE 0
                    END) AS [NonDraftCount]

                FROM [Rounds] r
                JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                JOIN [TournamentRoundMappings] trm ON trm.[SeasonId] = r.[SeasonId] AND trm.[RoundNumber] = r.[RoundNumber]
                WHERE l.[Id] = @LeagueId
                GROUP BY CASE WHEN trm.[Stages] LIKE '%Group%' THEN @GroupStage ELSE @KnockoutStage END
            )

            SELECT
                sa.[Stage],
                sa.[RoundsRemaining],
                sa.[RoundsCompleted]
            FROM
                [StageAggregates] sa
            WHERE
                sa.[NonDraftCount] > 0
            ORDER BY
                sa.[FirstRoundNumber]";

        var stages = await dbConnection.QueryAsync<StageRow>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                DraftStatus = nameof(RoundStatus.Draft),
                CompletedStatus = nameof(RoundStatus.Completed),
                GroupStage = nameof(TournamentStageGroup.GroupStage),
                KnockoutStage = nameof(TournamentStageGroup.KnockoutStage)
            });

        return stages.Select(s =>
        {
            var stage = Enum.Parse<TournamentStageGroup>(s.Stage);
            return new StageDto(stage, GetDisplayName(stage), s.RoundsRemaining, s.RoundsCompleted);
        });
    }

    private static string GetDisplayName(TournamentStageGroup stage) => stage switch
    {
        TournamentStageGroup.GroupStage => "Group Stage",
        _ => "Knockout Stage"
    };

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed record StageRow(string Stage, int RoundsRemaining, int RoundsCompleted);
}
