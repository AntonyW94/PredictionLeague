using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The administrator's member-management page for a league.
/// </summary>
public class FetchLeagueMembersQueryHandler(
    ILeagueMembersQuery membersQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<FetchLeagueMembersQuery, LeagueMembersPageDto>
{
    public async Task<LeagueMembersPageDto> Handle(
        FetchLeagueMembersQuery request,
        CancellationToken cancellationToken)
    {
        // A system administrator gets in without running the league, because Manage Leagues already offers them this page
        // for every public and private league - and because placing a member in a league they do not run is the one job
        // that needs it. The same waiver is in DeleteLeagueCommandHandler and UpdateLeagueMemberStatusCommandHandler, so
        // an administrator who can open the page can also act on what it shows.
        if (!request.IsAdmin)
            await membershipService.EnsureLeagueAdministratorAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await membersQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (data is null)
            throw new EntityNotFoundException("League", request.LeagueId);

        return new LeagueMembersPageDto
        {
            LeagueName = data.LeagueName,
            Members = data.Members
                .OrderBy(member => member.FirstName, StringComparer.InvariantCultureIgnoreCase)
                .ThenBy(member => member.LastName, StringComparer.InvariantCultureIgnoreCase)
                .Select(member => new LeagueMemberDto(
                    member.UserId,
                    PlayerDisplayName.Format(member.FirstName, member.LastName),
                    member.JoinedAtUtc,
                    member.Status))
                .ToList()
        };
    }
}
