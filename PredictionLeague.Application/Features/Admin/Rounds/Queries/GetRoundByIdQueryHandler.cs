using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class GetRoundByIdQueryHandler : IRequestHandler<GetRoundByIdQuery, RoundDetailsDto?>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchRepository _matchRepository;

    public GetRoundByIdQueryHandler(IRoundRepository roundRepository, IMatchRepository matchRepository)
    {
        _roundRepository = roundRepository;
        _matchRepository = matchRepository;
    }

    public async Task<RoundDetailsDto?> Handle(GetRoundByIdQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.Id);
        if (round == null)
            return null;

        var matches = await _matchRepository.GetByRoundIdAsync(request.Id);

        var response = new RoundDetailsDto
        {
            Round = new RoundDto
            {
                Id = round.Id,
                SeasonId = round.SeasonId,
                RoundNumber = round.RoundNumber,
                StartDate = round.StartDate,
                Deadline = round.Deadline
            },
            Matches = matches.Select(m => new MatchInRoundDto
            (
                m.Id,
                m.MatchDateTime,
                m.HomeTeamId,
                m.HomeTeam?.Name ?? "N/A",
                m.HomeTeam?.LogoUrl ?? "",
                m.AwayTeamId,
                m.AwayTeam?.Name ?? "N/A",
                m.AwayTeam?.LogoUrl ?? "",
                m.ActualHomeTeamScore,
                m.ActualAwayTeamScore,
                m.Status
            )).ToList()
        };

        return response;
    }
}