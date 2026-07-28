using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Queries;

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

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
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
