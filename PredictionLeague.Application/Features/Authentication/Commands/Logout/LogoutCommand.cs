using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Authentication.Commands.Logout;

public record LogoutCommand(
    string UserId,
    string? RefreshToken
) : IRequest, ITransactionalRequest;