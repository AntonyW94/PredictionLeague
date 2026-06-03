using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Boosts.Commands;

public class SetLeagueBoostRulesCommandHandler(
    ILeagueRepository leagueRepository,
    ILeagueBoostRuleRepository boostRuleRepository,
    IUserManager userManager,
    ILogger<SetLeagueBoostRulesCommandHandler> logger) : IRequestHandler<SetLeagueBoostRulesCommand>
{
    public async Task Handle(SetLeagueBoostRulesCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        var settingUser = await userManager.FindByIdAsync(request.UserId);
        var isSiteAdmin = settingUser != null && await userManager.IsInRoleAsync(settingUser, RoleNames.Administrator);
        var isLeagueAdmin = league.AdministratorUserId == request.UserId;

        var alreadySet = await boostRuleRepository.HasRulesAsync(request.LeagueId, cancellationToken);

        if (alreadySet && !isSiteAdmin)
            throw new InvalidOperationException("The league's boosts have already been set and can only be changed by a site administrator.");

        if (!alreadySet && !isLeagueAdmin && !isSiteAdmin)
            throw new UnauthorizedAccessException("Only the league administrator can set the league's boosts.");

        await boostRuleRepository.SetRulesAsync(request.LeagueId, request.Selections, cancellationToken);

        logger.LogInformation("League (ID: {LeagueId}) boost rules set by user (ID: {UserId}){Override}", request.LeagueId, request.UserId, alreadySet ? " as a site-admin override" : string.Empty);
    }
}
