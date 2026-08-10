using MediatR;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// The boost catalogue, shown when configuring a league's boost rules.
///
/// No longer carries SQL, and therefore no longer carries
/// <c>[ExcludeFromCodeCoverage]</c>: what is left is the ordering rule and the mapping, both of which a
/// unit test can reach. The read itself is <see cref="IBoostCatalogueQuery"/>, whose SQL lives in the
/// persistence adapter and is covered by the conformance suite.
/// </summary>
public class GetBoostCatalogueQueryHandler(IBoostCatalogueQuery catalogueQuery)
    : IRequestHandler<GetBoostCatalogueQuery, List<BoostCatalogueItemDto>>
{
    public async Task<List<BoostCatalogueItemDto>> Handle(GetBoostCatalogueQuery request, CancellationToken cancellationToken)
    {
        var rows = await catalogueQuery.ExecuteAsync(cancellationToken);

        return rows
            // Alphabetical by name, ordinal-ignore-case. Sorted here rather than in the query because
            // ORDER BY defers to the database's collation: the same rows could arrive in a different order
            // from a different adapter, or from the same adapter on a differently-collated database. Doing
            // it in C# makes the page's order a property of the application instead.
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new BoostCatalogueItemDto
            {
                Code = r.Code,
                Name = r.Name,
                Description = r.Description,
                Tooltip = r.Tooltip,
                Scope = r.Scope,
                ImageUrl = r.ImageUrl,
                SelectedImageUrl = r.SelectedImageUrl,
                DisabledImageUrl = r.DisabledImageUrl
            })
            .ToList();
    }
}
