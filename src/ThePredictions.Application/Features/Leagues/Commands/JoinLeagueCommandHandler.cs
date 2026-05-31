using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonAccessService seasonAccessService, IMediator mediator, IDateTimeProvider dateTimeProvider) : IRequestHandler<JoinLeagueCommand>
{
    public async Task Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await FetchLeagueAsync(request, cancellationToken);

        Guard.Against.EntityNotFound(request.LeagueId ?? 0, league, "League");

        await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league!.SeasonId, cancellationToken);

        league.AddMember(request.JoiningUserId, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);
        await NotifyAdminAsync(league, request, cancellationToken);
    }

    private async Task<League?> FetchLeagueAsync(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            return await leagueRepository.GetByIdAsync(request.LeagueId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.EntryCode))
            return await leagueRepository.GetByEntryCodeAsync(request.EntryCode, cancellationToken);

        throw new InvalidOperationException("Either a LeagueId or an EntryCode must be provided.");
    }

    private async Task NotifyAdminAsync(League league, JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (league.Members.Any(m => m.UserId == request.JoiningUserId))
        {
            await mediator.Send(new NotifyLeagueAdminOfJoinRequestCommand(
                league.Id,
                request.JoiningUserFirstName,
                request.JoiningUserLastName), cancellationToken);
        }
    }
}