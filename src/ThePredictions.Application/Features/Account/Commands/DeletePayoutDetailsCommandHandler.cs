using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Account.Commands;

public class DeletePayoutDetailsCommandHandler(IUserPayoutDetailsRepository payoutDetailsRepository)
    : IRequestHandler<DeletePayoutDetailsCommand>
{
    public async Task Handle(DeletePayoutDetailsCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);

        await payoutDetailsRepository.DeleteAsync(request.UserId, cancellationToken);
    }
}
