using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueDashboardQueryHandler : IRequestHandler<GetLeagueDashboardQuery, LeagueDashboardDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetLeagueDashboardQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<LeagueDashboardDto?> Handle(GetLeagueDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!request.IsAdmin)
        {
            const string authSql = @"
                SELECT COUNT(1) FROM [LeagueMembers] 
                WHERE [LeagueId] = @LeagueId AND [UserId] = @UserId AND [Status] = @ApprovedStatus;";

            var isMember = await _dbConnection.QuerySingleOrDefaultAsync<bool>(authSql, cancellationToken, new
            {
                request.LeagueId,
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            });

            if (!isMember)
                return null;
        }
        
        var leagueName = await _dbConnection.QuerySingleOrDefaultAsync<string>("SELECT [Name] FROM [Leagues] WHERE [Id] = @LeagueId", cancellationToken, new { request.LeagueId });
        if (leagueName == null) 
            return null;

        const string roundsSql = @"
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] m WHERE m.[RoundId] = r.[Id]) as MatchCount
            FROM
                [Rounds] r
            JOIN
                [Leagues] l ON r.[SeasonId] = l.[SeasonId]
            WHERE
                l.[Id] = @LeagueId
                AND r.[Status] IN (@PublishedStatus, @InProgressStatus, @CompletedStatus)
            ORDER BY
                r.[RoundNumber] DESC;";

        var parameters = new
        {
            request.LeagueId,
            PublishedStatus = nameof(RoundStatus.Published),
            InProgressStatus = nameof(RoundStatus.InProgress),
            CompletedStatus = nameof(RoundStatus.Completed)
        };
        var rounds = await _dbConnection.QueryAsync<RoundDto>(roundsSql, cancellationToken, parameters);

        return new LeagueDashboardDto
        {
            LeagueName = leagueName,
            ViewableRounds = rounds.ToList()
        };
    }
}