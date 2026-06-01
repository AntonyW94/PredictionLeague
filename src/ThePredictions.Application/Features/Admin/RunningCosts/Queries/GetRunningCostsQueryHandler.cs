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

        return await dbConnection.QueryAsync<RunningCostDto>(sql, cancellationToken);
    }
}
