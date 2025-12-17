using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Constants;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;
using PredictionLeague.Domain.Services;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IEntryCodeUniquenessChecker _uniquenessChecker;

    public CreateLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository, IEntryCodeUniquenessChecker uniquenessChecker)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _uniquenessChecker = uniquenessChecker;
    }

    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        var league = League.Create(
             request.SeasonId,
             request.Name,
             request.CreatingUserId,
             request.EntryDeadline,
             PublicLeagueSettings.PointsForExactScore, //TODO: Add this to the front end and CreateLeagueCommand
             PublicLeagueSettings.PointsForCorrectResult, //TODO: Add this to the front end and CreateLeagueCommand
             request.Price,
             season
         );

        await league.GenerateEntryCode(_uniquenessChecker, cancellationToken);
        
        var createdLeague = await _leagueRepository.CreateAsync(league, cancellationToken);

        return new LeagueDto(
            createdLeague.Id,
            createdLeague.Name,
            season.Name,
            1,
            createdLeague.Price,
            createdLeague.EntryCode ?? "Public",
            createdLeague.EntryDeadline
        );
    }
}