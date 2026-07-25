using MediatR;

namespace ThePredictions.Application.Features.Authentication.Commands.ResendConfirmation;

public record ResendConfirmationCommand(string UserId) : IRequest<Unit>;
