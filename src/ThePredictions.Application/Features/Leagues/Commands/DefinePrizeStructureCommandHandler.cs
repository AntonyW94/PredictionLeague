using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class DefinePrizeStructureCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository, IUserManager userManager, IDateTimeProvider dateTimeProvider) : IRequestHandler<DefinePrizeStructureCommand>
{
    public async Task Handle(DefinePrizeStructureCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");
       
        var definingUser = await userManager.FindByIdAsync(request.DefiningUserId);
        var isSiteAdmin = definingUser != null && await userManager.IsInRoleAsync(definingUser, RoleNames.Administrator);

        // Superseded by the prize scheme + deadline freeze (ADR-0011); retained only as a
        // site-admin manual override for edge cases, not the primary path.
        if (!isSiteAdmin)
            throw new UnauthorizedAccessException("The prize structure is now derived from the prize scheme; only a site administrator can set it manually.");

        if (league.EntryDeadlineUtc > dateTimeProvider.UtcNow)
            throw new BusinessRuleViolationException("The prize structure cannot be defined until after the entry deadline has passed.");

        var totalPrizePot = league.Price * league.Members.Count;
        var totalAllocatedPrizes = request.PrizeSettings.Sum(p => p.PrizeAmount * p.Multiplier);

        if (totalAllocatedPrizes != totalPrizePot)
            throw new BusinessRuleViolationException("The total allocated prize money must equal the total prize pot.");

        var prizeSettings = request.PrizeSettings.Select(p => LeaguePrizeSetting.Create(
            request.LeagueId,
            p.PrizeType,
            p.Rank,
            p.PrizeAmount
        )).ToList();

        league.DefinePrizes(prizeSettings);

        await leagueRepository.UpdateAsync(league, cancellationToken);
    }
}