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
            WITH ActiveMemberCount AS (
                SELECT
                    l.[SeasonId],
                    COUNT(DISTINCT lm.[UserId]) AS MemberCount
                FROM [LeagueMembers] lm
                JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
                WHERE l.[SeasonId] = (SELECT [SeasonId] FROM [Rounds] WHERE [Id] = @Id) AND lm.[Status] = @ApprovedStatus
                GROUP BY l.[SeasonId]
            )

            SELECT
                r.[Id] AS RoundId,
                r.[SeasonId],
                r.[RoundNumber],
                r.[ApiRoundName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                m.[Id] AS MatchId,
                m.[MatchDateTimeUtc],
                m.[HomeTeamId],
                ht.[Name] AS HomeTeamName,
                ht.[ShortName] AS HomeTeamShortName,
                ht.[Abbreviation] AS HomeTeamAbbreviation,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                m.[AwayTeamId],
                at.[Name] AS AwayTeamName,
                at.[ShortName] AS AwayTeamShortName,
                at.[Abbreviation] AS AwayTeamAbbreviation,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                m.[ActualHomeTeamScore],
                m.[ActualAwayTeamScore],
                m.[Status] AS 'MatchStatus'
            FROM [Rounds] r
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [ActiveMemberCount] amc ON r.[SeasonId] = amc.[SeasonId]
            WHERE r.[Id] = @Id;";

        var queryResult = await _dbConnection.QueryAsync<RoundQueryResult>(sql, cancellationToken, new { request.Id, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        var results = queryResult.ToList();
        if (!results.Any())
            return null;
        
        var firstRow = results.First();
        var roundDto = new RoundDto(
            firstRow.RoundId,
            firstRow.SeasonId,
            firstRow.RoundNumber,
            firstRow.ApiRoundName,
            firstRow.StartDateUtc,
            firstRow.DeadlineUtc,
            Enum.Parse<RoundStatus>(firstRow.Status),
            results.Count(r => r.MatchId.HasValue)
        );

        var roundDetails = new RoundDetailsDto
        {
            Round = roundDto,
            Matches = results
                .Where(r => r.MatchId.HasValue)
                .Select(r => new MatchInRoundDto(
                    r.MatchId!.Value,
                    r.MatchDateTimeUtc!.Value,
                    r.HomeTeamId!.Value,
                    r.HomeTeamName,
                    r.HomeTeamShortName,
                    r.HomeTeamAbbreviation,
                    r.HomeTeamLogoUrl,
                    r.AwayTeamId!.Value,
                    r.AwayTeamName,
                    r.AwayTeamShortName,
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
        string ApiRoundName,
        DateTime StartDateUtc,
        DateTime DeadlineUtc,
        string Status,
        int? MatchId,
        DateTime? MatchDateTimeUtc,
        int? HomeTeamId,
        string HomeTeamName,
        string HomeTeamShortName,
        string HomeTeamAbbreviation,
        string? HomeTeamLogoUrl,
        int? AwayTeamId,
        string AwayTeamName,
        string AwayTeamShortName,
        string AwayTeamAbbreviation,
        string? AwayTeamLogoUrl,
        int? ActualHomeTeamScore,
        int? ActualAwayTeamScore,
        string? MatchStatus
    );
}