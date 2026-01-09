using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class DefinePrizeStructureCommandHandler : IRequestHandler<DefinePrizeStructureCommand>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUserManager _userManager;

    public DefinePrizeStructureCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository, IUserManager userManager)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _userManager = userManager;
    }

    public async Task Handle(DefinePrizeStructureCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        var season = await _seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");
       
        var definingUser = await _userManager.FindByIdAsync(request.DefiningUserId);
        var isSiteAdmin = definingUser != null && await _userManager.IsInRoleAsync(definingUser, RoleNames.Administrator);

        if (league.AdministratorUserId != request.DefiningUserId && !isSiteAdmin)
            throw new UnauthorizedAccessException("Only the league administrator can define the prize structure.");

        if (league.EntryDeadlineUtc > DateTime.UtcNow)
            throw new InvalidOperationException("The prize structure cannot be defined until after the entry deadline has passed.");

        var totalPrizePot = league.Price * league.Members.Count;
        var totalAllocatedPrizes = request.PrizeSettings.Sum(p => p.PrizeAmount * p.Multiplier);

        if (totalAllocatedPrizes != totalPrizePot)
            throw new InvalidOperationException("The total allocated prize money must equal the total prize pot.");

        var prizeSettings = request.PrizeSettings.Select(p => LeaguePrizeSetting.Create(
            request.LeagueId,
            p.PrizeType,
            p.Rank,
            p.PrizeAmount
        )).ToList();

        league.DefinePrizes(prizeSettings);

        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }
}