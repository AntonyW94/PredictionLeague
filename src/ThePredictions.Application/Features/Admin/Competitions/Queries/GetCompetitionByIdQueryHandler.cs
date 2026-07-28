using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

public class GetCompetitionByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetCompetitionByIdQuery, CompetitionDto?>
{
    public async Task<CompetitionDto?> Handle(GetCompetitionByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                c.[Id],
                c.[Code],
                c.[Name],
                c.[Type],
                c.[LogoUrl],
                c.[Description],
                c.[ApiLeagueId],
                (SELECT COUNT(*) FROM [Seasons] s WHERE s.[CompetitionId] = c.[Id]) AS SeasonCount
            FROM
                [Competitions] c
            WHERE
                c.[Id] = @Id;";

        var competition = await dbConnection.QuerySingleOrDefaultAsync<CompetitionQueryResult>(sql, cancellationToken, new { request.Id });

        return competition is null
            ? null
            : new CompetitionDto(
                competition.Id,
                competition.Code,
                competition.Name,
                competition.Type,
                competition.LogoUrl,
                competition.Description,
                competition.ApiLeagueId,
                competition.SeasonCount);
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record CompetitionQueryResult(
        int Id,
        string Code,
        string Name,
        int Type,
        string? LogoUrl,
        string? Description,
        int? ApiLeagueId,
        int SeasonCount);
}
