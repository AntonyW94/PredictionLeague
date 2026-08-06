using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetExactScoresLeaderboardQueryHandler(
    IApplicationReadDbConnection connection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetExactScoresLeaderboardQuery, ExactScoresLeaderboardDto>
{
    public async Task<ExactScoresLeaderboardDto> Handle(GetExactScoresLeaderboardQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);
        const string entriesSql = @"
            DECLARE @SeasonId int = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId);

            SELECT
                RANK() OVER (ORDER BY ISNULL(exact_scores.[Total], 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName],
                ISNULL(exact_scores.[Total], 0) AS [ExactScoresCount],
                u.[Id] AS [UserId]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            OUTER APPLY (
                SELECT
                    SUM(rr.[ExactScoreCount]) AS [Total]
                FROM
                    [RoundResults] rr
                INNER JOIN
                    [Rounds] r ON r.[Id] = rr.[RoundId]
                WHERE
                    rr.[UserId] = lm.[UserId]
                    AND r.[SeasonId] = @SeasonId
            ) exact_scores
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus
            ORDER BY
                [ExactScoresCount] DESC,
                [PlayerName]";

        var leaderboardEntries = await connection.QueryAsync<ExactScoresLeaderboardQueryResult>(entriesSql, cancellationToken, new { request.LeagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        var leaderboard = new ExactScoresLeaderboardDto
        {
            Entries = leaderboardEntries
                .Select(e => new ExactScoresLeaderboardEntryDto
                {
                    Rank = e.Rank,
                    PlayerName = e.PlayerName,
                    ExactScoresCount = e.ExactScoresCount,
                    UserId = e.UserId
                })
                .ToList()
        };

        return leaderboard;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record ExactScoresLeaderboardQueryResult(
        long Rank,
        string PlayerName,
        int ExactScoresCount,
        string UserId);
}
