using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Users.Commands;

public class DeleteUserCommandHandler(
    IUserManager userManager,
    ILeagueRepository leagueRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        if (request.UserIdToDelete == request.DeletingUserId)
            throw new BusinessRuleViolationException("Administrators cannot delete their own account.");

        var userToDelete = await userManager.FindByIdAsync(request.UserIdToDelete);
        Guard.Against.EntityNotFound(request.UserIdToDelete, userToDelete, "User");

        var leaguesToReassign = await leagueRepository.GetLeaguesByAdministratorIdAsync(request.UserIdToDelete, cancellationToken);
        var leaguesList = leaguesToReassign.ToList();

        if (leaguesList.Any())
        {
            if (string.IsNullOrWhiteSpace(request.NewAdministratorId))
                throw new BusinessRuleViolationException("This user is the administrator of one or more leagues. You must select a new administrator to re-assign them to before deleting this account.");

            var newAdmin = await userManager.FindByIdAsync(request.NewAdministratorId);
            Guard.Against.NotFound(request.NewAdministratorId, newAdmin, "New Administrator User");

            foreach (var league in leaguesList)
            {
                league.ReassignAdministrator(request.NewAdministratorId);
                await leagueRepository.UpdateAsync(league, cancellationToken);
            }
        }

        var result = await userManager.DeleteAsync(userToDelete);
        if (!result.Succeeded)
            throw new Exception($"Failed to delete user: {string.Join(", ", result.Errors)}");
    }
}