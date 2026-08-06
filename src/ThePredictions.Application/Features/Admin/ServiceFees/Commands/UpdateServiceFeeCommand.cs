using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateServiceFeeCommand(
    ServiceFeeProvider Provider,
    decimal PercentFee,
    decimal FixedFee) : IRequest;
