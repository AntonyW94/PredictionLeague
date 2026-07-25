using MediatR;

namespace ThePredictions.Application.Features.Authentication.Commands.RequestPasswordReset;

public record RequestPasswordResetCommand(string Email) : IRequest<Unit>;
