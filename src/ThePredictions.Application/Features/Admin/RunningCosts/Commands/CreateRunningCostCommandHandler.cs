using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

public class CreateRunningCostCommandHandler(IRunningCostRepository runningCostRepository, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateRunningCostCommand>
{
    public async Task Handle(CreateRunningCostCommand request, CancellationToken cancellationToken)
    {
        var runningCost = RunningCost.Create(
            request.Name,
            request.Amount,
            request.Frequency,
            request.StartDateUtc,
            request.EndDateUtc,
            request.Notes,
            dateTimeProvider);

        await runningCostRepository.AddAsync(runningCost, cancellationToken);
    }
}
