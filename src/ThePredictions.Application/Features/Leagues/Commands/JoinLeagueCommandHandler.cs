using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler(ILeagueRepository leagueRepository, ILeagueStatsRepository leagueStatsRepository, ISeasonAccessService seasonAccessService, IBackgroundCommandDispatcher backgroundCommandDispatcher, IDateTimeProvider dateTimeProvider) : IRequestHandler<JoinLeagueCommand, int>
{
    public async Task<int> Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await FetchLeagueAsync(request, cancellationToken);

        Guard.Against.EntityNotFound(request.LeagueId ?? 0, league, "League");

        // Private leagues must be joined with their entry code. Joining by league id (the public path) is
        // rejected for private leagues so that listing them in Available Leagues never exposes a way to
        // bypass the code.
        if (request.LeagueId.HasValue && league!.EntryCode is not null)
            throw new BusinessRuleViolationException("This league requires an entry code to join.");

        await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league!.SeasonId, cancellationToken);

        league.AddMember(request.JoiningUserId, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);

        // A league that does not require approval admits the joiner straight away, which reorders every
        // existing member's cached rank. A league that does require approval leaves them Pending, and
        // pending members are not ranked, so there is nothing to recompute until they are approved.
        var joinedAsApproved = league.Members.Any(m => m.UserId == request.JoiningUserId && m.Status == LeagueMemberStatus.Approved);
        if (joinedAsApproved)
            await leagueStatsRepository.RefreshLeagueAsync(league.Id, cancellationToken);

        Notify(league, request);

        return league.Id;
    }

    private async Task<League?> FetchLeagueAsync(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            return await leagueRepository.GetByIdAsync(request.LeagueId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.EntryCode))
            return await leagueRepository.GetByEntryCodeAsync(request.EntryCode, cancellationToken);

        throw new BusinessRuleViolationException("Either a LeagueId or an EntryCode must be provided.");
    }

    /// <summary>
    /// Tells whoever needs to know, without making the joiner wait for it.
    /// </summary>
    /// <remarks>
    /// Dispatched rather than sent. Both branches end at Brevo, a third party over the network, and awaiting
    /// that here put the whole round trip inside the join response: 5107ms of a 5121ms response in one CI run.
    /// The join is committed by this point and its id is about to be returned, so there is nothing left for the
    /// player to wait for. A send that fails is logged by the dispatcher and does not reach them - which is the
    /// intent either way, since an email nobody received is not a reason to refuse a join that happened.
    /// </remarks>
    private void Notify(League league, JoinLeagueCommand request)
    {
        // Always present: AddMember above either added them or threw, so no null guard.
        var member = league.Members.First(m => m.UserId == request.JoiningUserId);

        // Auto-approved (the league does not require approval): tell the joiner they can take part.
        // Otherwise the request is pending: tell the admin there is someone to approve.
        if (member.Status == LeagueMemberStatus.Approved)
        {
            backgroundCommandDispatcher.Dispatch(new NotifyMemberOfLeagueApprovalCommand(
                request.JoiningUserId,
                league.Id,
                league.Name,
                league.SeasonId));
        }
        else
        {
            backgroundCommandDispatcher.Dispatch(new NotifyLeagueAdminOfJoinRequestCommand(
                league.AdministratorUserId,
                league.Name,
                league.SeasonId,
                request.JoiningUserFirstName,
                request.JoiningUserLastName));
        }
    }
}