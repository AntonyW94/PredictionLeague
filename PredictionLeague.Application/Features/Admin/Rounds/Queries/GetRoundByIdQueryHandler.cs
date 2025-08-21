using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class GetRoundByIdQueryHandler : IRequestHandler<GetRoundByIdQuery, RoundDetailsDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetRoundByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<RoundDetailsDto?> Handle(GetRoundByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH RoundPredictionCounts AS (
                SELECT
                    r.Id AS RoundId,
                    COUNT(DISTINCT up.UserId) AS PredictionsCount
                FROM Rounds r
                LEFT JOIN Matches m ON m.RoundId = r.Id
                LEFT JOIN UserPredictions up ON up.MatchId = m.Id
                WHERE r.Id = @Id
                GROUP BY r.Id
            ),
            ActiveMemberCount AS (
                SELECT
                    l.SeasonId,
                    COUNT(DISTINCT lm.UserId) AS MemberCount
                FROM LeagueMembers lm
                JOIN Leagues l ON lm.LeagueId = l.Id
                WHERE l.SeasonId = (SELECT SeasonId FROM Rounds WHERE Id = @Id) AND lm.Status = 'Approved'
                GROUP BY l.SeasonId
            )
            SELECT
                r.[Id] AS RoundId,
                r.[SeasonId],
                r.[RoundNumber],
                r.[StartDate],
                r.[Deadline],
                r.[Status],
                m.[Id] AS MatchId,
                m.[MatchDateTime],
                m.[HomeTeamId],
                ht.[Name] AS HomeTeamName,
                ht.[Abbreviation] AS HomeTeamAbbreviation,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                m.[AwayTeamId],
                at.[Name] AS AwayTeamName,
                at.[Abbreviation] AS AwayTeamAbbreviation,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                m.[ActualHomeTeamScore],
                m.[ActualAwayTeamScore],
                m.[Status] AS 'MatchStatus',
                CAST (   
                        CASE
                            WHEN rpc.[PredictionsCount] >= amc.[MemberCount] AND amc.[MemberCount] > 0 THEN 1
                            ELSE 0
                        END AS BIT
                    ) AS AllPredictionsIn
            FROM [Rounds] r
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [RoundPredictionCounts] rpc ON r.[Id] = rpc.[RoundId]
            LEFT JOIN [ActiveMemberCount] amc ON r.[SeasonId] = amc.[SeasonId]
            WHERE r.[Id] = @Id;";

        var queryResult = await _dbConnection.QueryAsync<RoundQueryResult>(sql, cancellationToken, new { request.Id });

        var results = queryResult.ToList();
        if (!results.Any())
            return null;
        
        var firstRow = results.First();
        var roundDto = new RoundWithAllPredictionsInDto(
            firstRow.RoundId,
            firstRow.SeasonId,
            firstRow.RoundNumber,
            firstRow.StartDate,
            firstRow.Deadline,
            Enum.Parse<RoundStatus>(firstRow.Status),
            results.Count(r => r.MatchId.HasValue),
            firstRow.AllPredictionsIn
        );

        var roundDetails = new RoundDetailsDto
        {
            Round = roundDto,
            Matches = results
                .Where(r => r.MatchId.HasValue)
                .Select(r => new MatchInRoundDto(
                    r.MatchId!.Value,
                    r.MatchDateTime!.Value,
                    r.HomeTeamId!.Value,
                    r.HomeTeamName,
                    r.HomeTeamAbbreviation,
                    r.HomeTeamLogoUrl,
                    r.AwayTeamId!.Value,
                    r.AwayTeamName,
                    r.AwayTeamAbbreviation,
                    r.AwayTeamLogoUrl,
                    r.ActualHomeTeamScore,
                    r.ActualAwayTeamScore,
                    Enum.Parse<MatchStatus>(r.MatchStatus!)
                )).ToList()
        };

        return roundDetails;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RoundQueryResult(
        int RoundId,
        int SeasonId,
        int RoundNumber,
        DateTime StartDate,
        DateTime Deadline,
        string Status,
        int? MatchId,
        DateTime? MatchDateTime,
        int? HomeTeamId,
        string HomeTeamName,
        string HomeTeamAbbreviation,
        string? HomeTeamLogoUrl,
        int? AwayTeamId,
        string AwayTeamName,
        string AwayTeamAbbreviation,
        string? AwayTeamLogoUrl,
        int? ActualHomeTeamScore,
        int? ActualAwayTeamScore,
        string? MatchStatus,
        bool AllPredictionsIn
    );
}