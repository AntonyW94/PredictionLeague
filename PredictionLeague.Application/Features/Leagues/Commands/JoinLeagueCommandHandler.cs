using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler : IRequestHandler<JoinLeagueCommand>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly IMediator _mediator;

    public JoinLeagueCommandHandler(ILeagueRepository leagueRepository, IMediator mediator)
    {
        _leagueRepository = leagueRepository;
        _mediator = mediator;
    }

    public async Task Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await FetchLeagueAsync(request, cancellationToken);

        Guard.Against.EntityNotFound(request.LeagueId ?? 0, league, "League");

        league.AddMember(request.JoiningUserId);

        await _leagueRepository.UpdateAsync(league, cancellationToken);
        await NotifyAdminAsync(league, request, cancellationToken);
    }

    private async Task<League?> FetchLeagueAsync(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            return await _leagueRepository.GetByIdAsync(request.LeagueId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.EntryCode))
            return await _leagueRepository.GetByEntryCodeAsync(request.EntryCode, cancellationToken);

        throw new InvalidOperationException("Either a LeagueId or an EntryCode must be provided.");
    }

    private async Task NotifyAdminAsync(League league, JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (league.Members.Any(m => m.UserId == request.JoiningUserId))
        {
            await _mediator.Send(new NotifyLeagueAdminOfJoinRequestCommand(
                league.Id,
                request.JoiningUserFirstName,
                request.JoiningUserLastName), cancellationToken);
        }
    }
}