using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthenticationResponse>;