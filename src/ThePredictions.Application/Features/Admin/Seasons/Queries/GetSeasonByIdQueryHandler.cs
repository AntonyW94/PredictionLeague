using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class GetSeasonByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetSeasonByIdQuery, SeasonDto?>
{
    public async Task<SeasonDto?> Handle(GetSeasonByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDateUtc],
                s.[EndDateUtc],
                s.[IsActive],
                s.[NumberOfRounds],
                s.[CompetitionId],
                c.[Name] AS CompetitionName,
                c.[Type] AS CompetitionType,
                c.[ApiLeagueId],
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id]) AS RoundCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'Draft') AS DraftCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'Published') AS PublishedCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'InProgress') AS InProgressCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'Completed') AS CompletedCount,
                (
                    SELECT COUNT(*)
                    FROM (
                        SELECT m.[HomeTeamId] AS TeamId
                        FROM [Matches] m
                        JOIN [Rounds] r ON m.[RoundId] = r.[Id]
                        WHERE r.[SeasonId] = s.[Id]
                            AND r.[RoundNumber] = (SELECT MIN(r2.[RoundNumber]) FROM [Rounds] r2 WHERE r2.[SeasonId] = s.[Id])
                            AND m.[HomeTeamId] IS NOT NULL
                        UNION
                        SELECT m.[AwayTeamId]
                        FROM [Matches] m
                        JOIN [Rounds] r ON m.[RoundId] = r.[Id]
                        WHERE r.[SeasonId] = s.[Id]
                            AND r.[RoundNumber] = (SELECT MIN(r2.[RoundNumber]) FROM [Rounds] r2 WHERE r2.[SeasonId] = s.[Id])
                            AND m.[AwayTeamId] IS NOT NULL
                    ) firstRoundTeams
                ) AS TeamCount,
                s.[PassStandardPrice],
                s.[PassPremiumPrice]
            FROM
                [Seasons] s
            JOIN
                [Competitions] c ON s.[CompetitionId] = c.[Id]
            WHERE
                s.[Id] = @Id";

        var season = await dbConnection.QuerySingleOrDefaultAsync<SeasonQueryResult>(sql, cancellationToken, new { request.Id });

        return season is null
            ? null
            : new SeasonDto(
                season.Id,
                season.Name,
                season.StartDateUtc,
                season.EndDateUtc,
                season.IsActive,
                season.NumberOfRounds,
                season.CompetitionId,
                season.CompetitionName,
                season.CompetitionType,
                season.ApiLeagueId,
                season.RoundCount,
                season.DraftCount,
                season.PublishedCount,
                season.InProgressCount,
                season.CompletedCount,
                season.TeamCount,
                season.PassStandardPrice,
                season.PassPremiumPrice);
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record SeasonQueryResult(
        int Id,
        string Name,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        bool IsActive,
        int NumberOfRounds,
        int CompetitionId,
        string CompetitionName,
        CompetitionType CompetitionType,
        int? ApiLeagueId,
        int RoundCount,
        int DraftCount,
        int PublishedCount,
        int InProgressCount,
        int CompletedCount,
        int TeamCount,
        decimal? PassStandardPrice,
        decimal? PassPremiumPrice);
}