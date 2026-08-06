using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Application.Features.Account.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetMyPayoutDetailsQuery(string UserId) : IRequest<MyPayoutDetailsDto>;
