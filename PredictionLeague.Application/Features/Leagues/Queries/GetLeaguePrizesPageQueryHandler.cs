using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeaguePrizesPageQueryHandler : IRequestHandler<GetLeaguePrizesPageQuery, LeaguePrizesPageDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetLeaguePrizesPageQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<LeaguePrizesPageDto?> Handle(GetLeaguePrizesPageQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Name] AS LeagueName,
                l.[EntryDeadline],
                l.[Price],
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.LeagueId = l.Id) AS MemberCount,
                s.[NumberOfRounds],
                s.[StartDate] AS SeasonStartDate,
                s.[EndDate] AS SeasonEndDate,
                ps.[PrizeType],
                ps.[Rank],
                ps.[PrizeAmount]
            FROM 
                [Leagues] l
            JOIN 
                [Seasons] s ON l.SeasonId = s.Id
            LEFT JOIN
                [LeaguePrizeSettings] ps ON l.Id = ps.LeagueId
            WHERE 
                l.Id = @LeagueId;";

        var queryResult = await _dbConnection.QueryAsync<PrizesQueryResult>(sql, cancellationToken, new { request.LeagueId });

        var results = queryResult.ToList();
        if (!results.Any())
            return null;
        
        var firstRow = results.First();
        var pageDto = new LeaguePrizesPageDto
        {
            LeagueName = firstRow.LeagueName,
            EntryDeadline = firstRow.EntryDeadline,
            Price = firstRow.Price,
            MemberCount = firstRow.MemberCount,
            NumberOfRounds = firstRow.NumberOfRounds,
            SeasonStartDate = firstRow.SeasonStartDate,
            SeasonEndDate = firstRow.SeasonEndDate,
            PrizeSettings = results
                .Where(r => r.PrizeType != null)
                .Select(r => new PrizeSettingDto(
                    Enum.Parse<PrizeType>(r.PrizeType!),
                    r.Rank!.Value,
                    r.PrizeAmount!.Value
                )).ToList()
        };

        return pageDto;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PrizesQueryResult(
        string LeagueName,
        DateTime EntryDeadline,
        decimal Price,
        int MemberCount,
        int NumberOfRounds,
        DateTime SeasonStartDate,
        DateTime SeasonEndDate,
        string? PrizeType,
        int? Rank,
        decimal? PrizeAmount
    );
}