﻿using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Guards.Season;
using System.Security.Authentication;

namespace PredictionLeague.Application.Features.Admin.Users.Commands;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserManager _userManager;
    private readonly ILeagueRepository _leagueRepository;

    public DeleteUserCommandHandler(IUserManager userManager, ILeagueRepository leagueRepository)
    {
        _userManager = userManager;
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsDeletingUserAdmin)
            throw new AuthenticationException("You do not have permission to delete users.");

        if (request.UserIdToDelete == request.DeletingUserId)
            throw new InvalidOperationException("Administrators cannot delete their own account.");

        var userToDelete = await _userManager.FindByIdAsync(request.UserIdToDelete);
        Guard.Against.EntityNotFound(request.UserIdToDelete, userToDelete, "User");

        var leaguesToReassign = await _leagueRepository.GetLeaguesByAdministratorIdAsync(request.UserIdToDelete, cancellationToken);
        var leaguesList = leaguesToReassign.ToList();

        if (leaguesList.Any())
        {
            if (string.IsNullOrWhiteSpace(request.NewAdministratorId))
                throw new InvalidOperationException("This user is the administrator of one or more leagues. You must select a new administrator to re-assign them to before deleting this account.");

            var newAdmin = await _userManager.FindByIdAsync(request.NewAdministratorId);
            Guard.Against.NotFound(request.NewAdministratorId, newAdmin, "New Administrator User");

            foreach (var league in leaguesList)
            {
                league.ReassignAdministrator(request.NewAdministratorId);
                await _leagueRepository.UpdateAsync(league, cancellationToken);
            }
        }

        var result = await _userManager.DeleteAsync(userToDelete);
        if (!result.Succeeded)
            throw new Exception($"Failed to delete user: {string.Join(", ", result.Errors)}");
    }
}