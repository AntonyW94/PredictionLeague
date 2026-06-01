using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

public class UpdateRunningCostCommandHandler(IRunningCostRepository runningCostRepository)
    : IRequestHandler<UpdateRunningCostCommand>
{
    public async Task Handle(UpdateRunningCostCommand request, CancellationToken cancellationToken)
    {
        var runningCost = await runningCostRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, runningCost, "RunningCost");

        runningCost!.Update(
            request.Name,
            request.Amount,
            request.Frequency,
            request.StartDateUtc,
            request.EndDateUtc,
            request.Payer,
            request.Notes);

        await runningCostRepository.UpdateAsync(runningCost, cancellationToken);
    }
}
