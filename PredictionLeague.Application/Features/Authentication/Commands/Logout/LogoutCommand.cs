using MediatR;

namespace PredictionLeague.Application.Features.Authentication.Commands.Logout;

public class LogoutCommand : IRequest
{
    public string? RefreshToken { get; init; }
}