using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueRoundsForDashboardQueryHandler : IRequestHandler<GetLeagueRoundsForDashboardQuery, IEnumerable<RoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetLeagueRoundsForDashboardQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<RoundDto>> Handle(GetLeagueRoundsForDashboardQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[StartDate],
                r.[Deadline],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] m WHERE m.[RoundId] = r.[Id]) as MatchCount
            FROM
                [Rounds] r
            JOIN
                [Leagues] l ON r.SeasonId = l.SeasonId
            WHERE
                l.[Id] = @LeagueId
                AND r.[Status] IN (@PublishedStatus, @CompletedStatus)
            ORDER BY
                r.[RoundNumber] DESC;";

        var parameters = new
        {
            request.LeagueId,
            PublishedStatus = nameof(RoundStatus.Published),
            CompletedStatus = nameof(RoundStatus.Completed)
        };

        return await _dbConnection.QueryAsync<RoundDto>(sql, cancellationToken, parameters);
    }
}