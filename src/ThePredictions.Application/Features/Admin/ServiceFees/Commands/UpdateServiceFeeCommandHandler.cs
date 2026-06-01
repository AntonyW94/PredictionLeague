using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Commands;

public class UpdateServiceFeeCommandHandler(IServiceFeeRepository serviceFeeRepository)
    : IRequestHandler<UpdateServiceFeeCommand>
{
    public async Task Handle(UpdateServiceFeeCommand request, CancellationToken cancellationToken)
    {
        var serviceFee = await serviceFeeRepository.GetByProviderAsync(request.Provider, cancellationToken);

        if (serviceFee is null)
        {
            // No row for this provider yet - create one, then apply the requested values.
            serviceFee = ServiceFee.CreateDefault(request.Provider);
            serviceFee.Update(request.PercentFee, request.FixedFee);
            await serviceFeeRepository.AddAsync(serviceFee, cancellationToken);
            return;
        }

        serviceFee.Update(request.PercentFee, request.FixedFee);
        await serviceFeeRepository.UpdateAsync(serviceFee, cancellationToken);
    }
}
