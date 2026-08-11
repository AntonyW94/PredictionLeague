using MediatR;
using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Queries;

/// <summary>What each payment provider charges.</summary>
public class GetServiceFeesQueryHandler(IServiceFeesQuery serviceFeesQuery)
    : IRequestHandler<GetServiceFeesQuery, IEnumerable<ServiceFeeDto>>
{
    public async Task<IEnumerable<ServiceFeeDto>> Handle(GetServiceFeesQuery request, CancellationToken cancellationToken)
    {
        var fees = await serviceFeesQuery.ExecuteAsync(cancellationToken);

        // Alphabetical by provider, with an explicit comparer rather than the database's collation.
        return fees
            .OrderBy(fee => fee.Provider, StringComparer.InvariantCultureIgnoreCase)
            .Select(fee => new ServiceFeeDto(fee.Provider, fee.PercentFee, fee.FixedFee))
            .ToList();
    }
}
