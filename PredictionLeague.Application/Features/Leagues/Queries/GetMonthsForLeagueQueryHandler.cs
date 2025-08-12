using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using System.Globalization;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetMonthsForLeagueQueryHandler : IRequestHandler<GetMonthsForLeagueQuery, IEnumerable<MonthDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMonthsForLeagueQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<MonthDto>> Handle(GetMonthsForLeagueQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT DISTINCT
                MONTH(r.[StartDate]) AS Month
            FROM [Rounds] r
            JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
            WHERE l.[Id] = @LeagueId
            ORDER BY Month;";

        var months = await _dbConnection.QueryAsync<int>(sql, cancellationToken, new { request.LeagueId });
        return months.Select(m => new MonthDto(m, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)));
    }
}