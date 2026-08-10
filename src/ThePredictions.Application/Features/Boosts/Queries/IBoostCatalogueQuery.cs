namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// Reads every boost definition the platform offers.
///
/// A port, owned by Application and implemented per adapter. Deliberately returns rows in no particular
/// order: the catalogue is presented alphabetically, and sorting is a rule the handler applies in C# rather
/// than something delegated to the database. That is not fussiness - <c>ORDER BY [Name]</c> sorts according
/// to the database's collation, so the same data could come back in a different order from a different
/// adapter, or from the same adapter on a differently-collated database.
/// </summary>
public interface IBoostCatalogueQuery
{
    Task<IReadOnlyList<BoostCatalogueRow>> ExecuteAsync(CancellationToken cancellationToken);
}
