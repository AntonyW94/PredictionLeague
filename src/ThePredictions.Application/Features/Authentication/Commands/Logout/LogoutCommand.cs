using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Authentication.Commands.Logout;

public record LogoutCommand(
    string UserId,
    string? RefreshToken = null
) : IRequest, ITransactionalRequest;