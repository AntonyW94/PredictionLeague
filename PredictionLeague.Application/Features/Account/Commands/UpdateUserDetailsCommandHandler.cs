using Ardalis.GuardClauses;
using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Common.Exceptions;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Account.Commands;

public class UpdateUserDetailsCommandHandler : IRequestHandler<UpdateUserDetailsCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateUserDetailsCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(UpdateUserDetailsCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, "User");
      
        user.UpdateDetails(request.FirstName, request.LastName, request.PhoneNumber);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new IdentityUpdateException(result.Errors);
    }
}