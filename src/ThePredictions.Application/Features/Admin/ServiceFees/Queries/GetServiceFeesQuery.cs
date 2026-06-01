using MediatR;
using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Queries;

public record GetServiceFeesQuery : IRequest<IEnumerable<ServiceFeeDto>>;
