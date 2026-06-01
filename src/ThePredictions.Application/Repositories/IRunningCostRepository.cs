using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IRunningCostRepository
{
    Task<RunningCost?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(RunningCost runningCost, CancellationToken cancellationToken);
    Task UpdateAsync(RunningCost runningCost, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
