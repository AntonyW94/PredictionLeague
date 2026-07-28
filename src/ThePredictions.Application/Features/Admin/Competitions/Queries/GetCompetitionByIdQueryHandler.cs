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

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
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
