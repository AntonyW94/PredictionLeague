using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Commands;

public record UpdateServiceFeeCommand(
    ServiceFeeProvider Provider,
    decimal PercentFee,
    decimal FixedFee) : IRequest;
