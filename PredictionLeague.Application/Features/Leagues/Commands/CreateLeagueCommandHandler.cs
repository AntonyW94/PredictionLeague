using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, League>
{
    private readonly ILeagueRepository _leagueRepository;

    public CreateLeagueCommandHandler(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task<League> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = new League
        {
            Name = request.Name,
            SeasonId = request.SeasonId,
            AdministratorUserId = request.CreatingUserId,
            EntryCode = string.IsNullOrWhiteSpace(request.EntryCode) ? null : request.EntryCode
        };

        await _leagueRepository.CreateAsync(league);

        var leagueMember = new LeagueMember
        {
            LeagueId = league.Id,
            UserId = request.CreatingUserId,
            JoinedAt = DateTime.UtcNow,
            Status = "Approved"
        };

        await _leagueRepository.AddMemberAsync(leagueMember);

        return league;
    }
}