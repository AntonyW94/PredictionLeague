using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateScoresForNextRoundCommandHandler : IRequestHandler<UpdateScoresForNextRoundCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFootballDataService _footballDataService;
    private readonly IMediator _mediator;

    public UpdateScoresForNextRoundCommandHandler(
        IRoundRepository roundRepository,
        IFootballDataService footballDataService,
        IMediator mediator)
    {
        _roundRepository = roundRepository;
        _footballDataService = footballDataService;
        _mediator = mediator;
    }

    public async Task Handle(UpdateScoresForNextRoundCommand request, CancellationToken cancellationToken)
    {
        var activeRound = await _roundRepository.GetOldestInProgressRoundAsync(request.SeasonId, cancellationToken);
        if (activeRound == null || !activeRound.Matches.Any())
            return;

        var matchesToCheck = activeRound.Matches
            .Where(m => m.MatchDateTimeUtc < DateTime.UtcNow && m.Status != MatchStatus.Completed)
            .ToList();

        if (!matchesToCheck.Any())
            return;
        
        var externalIds = matchesToCheck
            .Where(m => m.ExternalId.HasValue)
            .Select(m => m.ExternalId.GetValueOrDefault())
            .ToList();

        if (!externalIds.Any())
            return;

        var liveFixtures = (await _footballDataService.GetFixturesByIdsAsync(externalIds, cancellationToken)).ToList();
        if (!liveFixtures.Any()) 
            return;

        var matchResults = liveFixtures.Where(f => f.Fixture != null && f.Goals != null).Select(fixture =>
        {
            var localMatch = activeRound.Matches.First(m => m.ExternalId == fixture.Fixture!.Id);
            return new MatchResultDto(
                localMatch.Id,
                fixture.Goals!.Home.GetValueOrDefault(),
                fixture.Goals.Away.GetValueOrDefault(),
                GetMatchStatus(fixture.Fixture!.Status.Short)
            );
        }).ToList();

        if (matchResults.Any())
        {
            var updateCommand = new UpdateMatchResultsCommand(activeRound.Id, matchResults);
            await _mediator.Send(updateCommand, cancellationToken);
        }
    }

    private static MatchStatus GetMatchStatus(string apiStatus) => apiStatus switch
    {
        "FT" => MatchStatus.Completed,
        "HT" or "1H" or "2H" => MatchStatus.InProgress,
        _ => MatchStatus.Scheduled
    };
}