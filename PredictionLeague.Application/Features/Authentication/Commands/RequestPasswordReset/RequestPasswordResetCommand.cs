using MediatR;

namespace PredictionLeague.Application.Features.Authentication.Commands.RequestPasswordReset;

public record RequestPasswordResetCommand(string Email, string ResetUrlBase) : IRequest<Unit>;
