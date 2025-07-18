using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommand : IRequest<AuthenticationResponse>
{
    public string? RefreshToken { get; }

    public RefreshTokenCommand(string refreshToken)
    {
        RefreshToken = refreshToken;
    }
}
