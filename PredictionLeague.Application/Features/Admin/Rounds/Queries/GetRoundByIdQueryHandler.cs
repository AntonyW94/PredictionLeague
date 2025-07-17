using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Matches;
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
        var round = await _roundRepository.GetByIdAsync(request.RoundId);
        if (round == null)
            return null;

        var matches = await _matchRepository.GetByRoundIdAsync(request.RoundId);

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
            Matches = matches.Select(m => new MatchDto
            {
                Id = m.Id,
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam?.Name ?? "N/A",
                HomeTeamLogoUrl = m.HomeTeam?.LogoUrl ?? "",
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.Name ?? "N/A",
                AwayTeamLogoUrl = m.AwayTeam?.LogoUrl ?? "",
                MatchDateTime = m.MatchDateTime,
                ActualHomeScore = m.ActualHomeTeamScore,
                ActualAwayScore = m.ActualAwayTeamScore
            }).ToList()
        };

        return response;
    }
}