using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The choices offered by the "Add Member" dropdown on the league members page.
/// </summary>
/// <remarks>
/// System administrators only, not the league's own administrator. Placing a player in a league bypasses the entry
/// deadline, and this read is what tells the caller who is there to place - so it is gated the same way the command is,
/// rather than leaving a list of every pass holder's email address readable by anyone who runs a league.
/// </remarks>
public class GetLeagueJoinCandidatesQueryHandler(
    ILeagueJoinCandidatesQuery candidatesQuery,
    ICurrentUserService currentUserService) : IRequestHandler<GetLeagueJoinCandidatesQuery, List<LeagueJoinCandidateDto>>
{
    public async Task<List<LeagueJoinCandidateDto>> Handle(
        GetLeagueJoinCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var candidates = await candidatesQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (candidates is null)
            throw new EntityNotFoundException("League", request.LeagueId);

        return candidates
            .OrderBy(candidate => candidate.FirstName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(candidate => candidate.LastName, StringComparer.InvariantCultureIgnoreCase)
            .Select(candidate => new LeagueJoinCandidateDto(
                candidate.UserId,
                PlayerDisplayName.FormatFull(candidate.FirstName, candidate.LastName),
                candidate.Email))
            .ToList();
    }
}
