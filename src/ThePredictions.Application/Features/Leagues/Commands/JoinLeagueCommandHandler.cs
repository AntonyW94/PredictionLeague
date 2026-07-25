using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonAccessService seasonAccessService, IMediator mediator, IDateTimeProvider dateTimeProvider) : IRequestHandler<JoinLeagueCommand, int>
{
    public async Task<int> Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await FetchLeagueAsync(request, cancellationToken);

        Guard.Against.EntityNotFound(request.LeagueId ?? 0, league, "League");

        // Private leagues must be joined with their entry code. Joining by league id (the public path) is
        // rejected for private leagues so that listing them in Available Leagues never exposes a way to
        // bypass the code.
        if (request.LeagueId.HasValue && league!.EntryCode is not null)
            throw new InvalidOperationException("This league requires an entry code to join.");

        await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league!.SeasonId, cancellationToken);

        league.AddMember(request.JoiningUserId, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);
        await NotifyAsync(league, request, cancellationToken);

        return league.Id;
    }

    private async Task<League?> FetchLeagueAsync(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            return await leagueRepository.GetByIdAsync(request.LeagueId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.EntryCode))
            return await leagueRepository.GetByEntryCodeAsync(request.EntryCode, cancellationToken);

        throw new InvalidOperationException("Either a LeagueId or an EntryCode must be provided.");
    }

    private async Task NotifyAsync(League league, JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var member = league.Members.FirstOrDefault(m => m.UserId == request.JoiningUserId);
        if (member is null)
            return;

        // Auto-approved (the league does not require approval): tell the joiner they can take part.
        // Otherwise the request is pending: tell the admin there is someone to approve.
        if (member.Status == LeagueMemberStatus.Approved)
        {
            await mediator.Send(new NotifyMemberOfLeagueApprovalCommand(
                request.JoiningUserId,
                league.Id,
                league.Name,
                league.SeasonId), cancellationToken);
        }
        else
        {
            await mediator.Send(new NotifyLeagueAdminOfJoinRequestCommand(
                league.AdministratorUserId,
                league.Name,
                league.SeasonId,
                request.JoiningUserFirstName,
                request.JoiningUserLastName), cancellationToken);
        }
    }
}