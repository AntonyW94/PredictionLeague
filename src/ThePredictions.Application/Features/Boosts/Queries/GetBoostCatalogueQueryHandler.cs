using System.Diagnostics.CodeAnalysis;
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

        var items = await dbConnection.QueryAsync<BoostCatalogueItemQueryResult>(sql, cancellationToken);

        return items
            .Select(i => new BoostCatalogueItemDto
            {
                Code = i.Code,
                Name = i.Name,
                Description = i.Description,
                Tooltip = i.Tooltip,
                Scope = i.Scope,
                ImageUrl = i.ImageUrl,
                SelectedImageUrl = i.SelectedImageUrl,
                DisabledImageUrl = i.DisabledImageUrl
            })
            .ToList();
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record BoostCatalogueItemQueryResult(
        string Code,
        string Name,
        string? Description,
        string? Tooltip,
        string Scope,
        string? ImageUrl,
        string? SelectedImageUrl,
        string? DisabledImageUrl);
}
