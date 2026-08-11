using MediatR;
using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Queries;

/// <summary>The recurring costs of running the site.</summary>
public class GetRunningCostsQueryHandler(IRunningCostsQuery runningCostsQuery)
    : IRequestHandler<GetRunningCostsQuery, IEnumerable<RunningCostDto>>
{
    public async Task<IEnumerable<RunningCostDto>> Handle(GetRunningCostsQuery request, CancellationToken cancellationToken)
    {
        var costs = await runningCostsQuery.ExecuteAsync(cancellationToken);

        // Alphabetical, with an explicit comparer rather than the database's collation.
        return costs
            .OrderBy(cost => cost.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(cost => new RunningCostDto(
                cost.Id, cost.Name, cost.Amount, cost.Frequency, cost.StartDateUtc, cost.EndDateUtc, cost.Notes))
            .ToList();
    }
}
