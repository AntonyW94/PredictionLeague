﻿using MediatR;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Authentication.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LogoutCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(request.UserId, cancellationToken);
    }
}

