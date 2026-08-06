using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetRunningCostsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetRunningCostsQuery, IEnumerable<RunningCostDto>>
{
    public async Task<IEnumerable<RunningCostDto>> Handle(GetRunningCostsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [Id],
                [Name],
                [Amount],
                [Frequency],
                [StartDateUtc],
                [EndDateUtc],
                [Notes]
            FROM
                [RunningCosts]
            ORDER BY
                [Name];";

        var costs = await dbConnection.QueryAsync<RunningCostQueryResult>(sql, cancellationToken);

        return costs.Select(c => new RunningCostDto(
            c.Id,
            c.Name,
            c.Amount,
            c.Frequency,
            c.StartDateUtc,
            c.EndDateUtc,
            c.Notes));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RunningCostQueryResult(
        int Id,
        string Name,
        decimal Amount,
        string Frequency,
        DateTime StartDateUtc,
        DateTime? EndDateUtc,
        string? Notes);
}
