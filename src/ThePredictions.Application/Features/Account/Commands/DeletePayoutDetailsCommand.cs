using MediatR;

namespace ThePredictions.Application.Features.Account.Commands;

public record DeletePayoutDetailsCommand(string UserId) : IRequest;
