using MediatR;
using Microsoft.AspNetCore.Authentication;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;

public record LoginWithGoogleCommand(
    AuthenticateResult AuthenticateResult,
    string Source
) : IRequest<AuthenticationResponse>;