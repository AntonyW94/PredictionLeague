using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

public class DeleteRunningCostCommandHandler(IRunningCostRepository runningCostRepository)
    : IRequestHandler<DeleteRunningCostCommand>
{
    public async Task Handle(DeleteRunningCostCommand request, CancellationToken cancellationToken)
    {
        await runningCostRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
