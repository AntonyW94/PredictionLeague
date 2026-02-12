using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.Register;

public record RegisterCommand(string FirstName, string LastName, string Email, string Password) : IRequest<AuthenticationResponse>;