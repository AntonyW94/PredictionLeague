using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler : IRequestHandler<JoinLeagueCommand>
{
    private readonly ILogger<JoinLeagueCommandHandler> _logger;
    private readonly ILeagueRepository _leagueRepository;

    public JoinLeagueCommandHandler(ILogger<JoinLeagueCommandHandler> logger, ILeagueRepository leagueRepository)
    {
        _logger = logger;
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        League? league;

        if (request.LeagueId.HasValue)
            league = await _leagueRepository.GetByIdAsync(request.LeagueId.Value);
        else if (!string.IsNullOrWhiteSpace(request.EntryCode))
            league = await _leagueRepository.GetByEntryCodeAsync(request.EntryCode);
        else
            throw new InvalidOperationException("Either a LeagueId or an EntryCode must be provided.");

        if (league == null)
        {
            _logger.LogWarning("Failed attempt to join a league (League ID: {LeagueId}, Entry Code: {EntryCode}).", request.LeagueId, request.EntryCode);
            throw new KeyNotFoundException("The specified league could not be found.");
        }
        
        if (league.EntryDeadline.HasValue && league.EntryDeadline.Value < DateTime.UtcNow)
            throw new InvalidOperationException("The deadline to join this league has passed.");

        var members = await _leagueRepository.GetMembersByLeagueIdAsync(league.Id);
        if (members.Any(m => m.UserId == request.JoiningUserId))
            return;

        var newMember = new LeagueMember
        {
            LeagueId = league.Id,
            UserId = request.JoiningUserId,
            Status = "Pending",
            JoinedAt = DateTime.UtcNow
        };

        await _leagueRepository.AddMemberAsync(newMember);
    }
}