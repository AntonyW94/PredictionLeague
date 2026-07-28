using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetOverallLeaderboardQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetOverallLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetOverallLeaderboardQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        const string sql = @"
            SELECT
                RANK() OVER (ORDER BY COALESCE(SUM(lrr.[BoostedPoints]), 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName],
                COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints],
                u.[Id] AS [UserId],
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Rounds] r
                    JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @CompletedStatus
                ) THEN stats.[SnapshotOverallRank] ELSE NULL END AS [SnapshotRank],
                CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Rounds] r
                    JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @InProgressStatus
                ) THEN 1 ELSE 0 END AS bit) AS [IsRoundInProgress]
            
            FROM 
	            [LeagueMembers] lm
            
            JOIN 
	            [AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN 
	            [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId
            LEFT JOIN 
                [LeagueMemberStats] stats ON lm.[LeagueId] = stats.[LeagueId] AND lm.[UserId] = stats.[UserId]
           
            WHERE 
	            lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus

            GROUP BY 
	            u.[FirstName], 
                u.[LastName], 
                u.[Id],
                stats.[SnapshotOverallRank]

            ORDER BY 
	            [Rank], 
                [PlayerName];";

        var entries = await dbConnection.QueryAsync<OverallLeaderboardQueryResult>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(RoundStatus.InProgress),
                CompletedStatus = nameof(RoundStatus.Completed)
            }
        );

        return entries.Select(e => new LeaderboardEntryDto
        {
            Rank = e.Rank,
            PlayerName = e.PlayerName,
            TotalPoints = e.TotalPoints,
            UserId = e.UserId,
            SnapshotRank = e.SnapshotRank,
            IsRoundInProgress = e.IsRoundInProgress
        });
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record OverallLeaderboardQueryResult(
        long Rank,
        string PlayerName,
        int? TotalPoints,
        string UserId,
        long? SnapshotRank,
        bool IsRoundInProgress);
}
