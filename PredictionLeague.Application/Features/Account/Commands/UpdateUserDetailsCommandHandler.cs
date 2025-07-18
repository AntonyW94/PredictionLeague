using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Common.Exceptions;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Account.Commands;

public class UpdateUserDetailsCommandHandler : IRequestHandler<UpdateUserDetailsCommand>
{
    private readonly ILogger<UpdateUserDetailsCommandHandler> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateUserDetailsCommandHandler(ILogger<UpdateUserDetailsCommandHandler> logger, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _userManager = userManager;
    }

    public async Task Handle(UpdateUserDetailsCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            _logger.LogWarning("User (ID: {UserId}) not found during detail update.", request.UserId);
            throw new KeyNotFoundException($"User (ID: {request.UserId}) not found.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("User (ID: {UserId}) errored during detail update.", request.UserId);
            throw new IdentityUpdateException(result.Errors);
        }
    }
}