using Ardalis.GuardClauses;
using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Users.Commands;

public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateUserRoleCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, "User");

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        var result = await _userManager.AddToRoleAsync(user, request.NewRole);
        if (!result.Succeeded) 
            throw new Exception($"Failed to update role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}