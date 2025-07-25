using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler : IRequestHandler<JoinLeagueCommand>
{
    private readonly ILeagueRepository _leagueRepository;

    public JoinLeagueCommandHandler(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await FetchLeague(request, cancellationToken);
        
        Guard.Against.EntityNotFound(request.LeagueId ?? 0, league, "League");

        league.AddMember(request.JoiningUserId);
      
        await _leagueRepository.UpdateAsync(league, cancellationToken);
    }

    private async Task<League?> FetchLeague(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            return await _leagueRepository.GetByIdAsync(request.LeagueId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.EntryCode))
            return await _leagueRepository.GetByEntryCodeAsync(request.EntryCode, cancellationToken);

        throw new InvalidOperationException("Either a LeagueId or an EntryCode must be provided.");
    }
}