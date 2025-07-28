using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class DefinePrizeStructureCommandHandler : IRequestHandler<DefinePrizeStructureCommand>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public DefinePrizeStructureCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task Handle(DefinePrizeStructureCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        var season = await _seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");

        if (league.AdministratorUserId != request.DefiningUserId)
            throw new UnauthorizedAccessException("Only the league administrator can define the prize structure.");

        if (league.EntryDeadline > DateTime.UtcNow)
            throw new InvalidOperationException("The prize structure cannot be defined until after the entry deadline has passed.");

        var totalPrizePot = league.Price * league.Members.Count;
        var totalAllocatedPrizes = request.PrizeSettings.Sum(p => p.PrizeAmount);

        if (totalAllocatedPrizes != totalPrizePot)
            throw new InvalidOperationException("The total allocated prize money must equal the total prize pot.");

        var prizeSettings = request.PrizeSettings.Select(p => LeaguePrizeSetting.Create(
            request.LeagueId,
            p.PrizeType,
            p.Rank,
            p.PrizeAmount,
            p.PrizeDescription,
            p.Month,
            p.RoundNumber
        )).ToList();

        league.DefinePrizes(prizeSettings);

        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }
}