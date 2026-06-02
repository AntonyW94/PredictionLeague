using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Queries;

public class GetBoostCatalogueQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetBoostCatalogueQuery, List<BoostCatalogueItemDto>>
{
    public async Task<List<BoostCatalogueItemDto>> Handle(GetBoostCatalogueQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                bd.[Code],
                bd.[Name],
                bd.[Description],
                bd.[Tooltip],
                bd.[Scope],
                bd.[ImageUrl],
                bd.[SelectedImageUrl],
                bd.[DisabledImageUrl]
            FROM
                [BoostDefinitions] bd
            ORDER BY
                bd.[Name];";

        var items = await dbConnection.QueryAsync<BoostCatalogueItemDto>(sql, cancellationToken);
        return items.ToList();
    }
}
