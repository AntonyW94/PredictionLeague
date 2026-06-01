using MediatR;
using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Application.Features.Account.Queries;

public record GetMyPayoutDetailsQuery(string UserId) : IRequest<MyPayoutDetailsDto>;
