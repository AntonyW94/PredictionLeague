namespace ThePredictions.Application.Features.Admin.RunningCosts.Queries;

/// <summary>Reads every running cost, in no order.</summary>
public interface IRunningCostsQuery
{
    Task<IReadOnlyList<RunningCostRow>> ExecuteAsync(CancellationToken cancellationToken);
}
