using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One league's settings, as its members and administrator see them.
/// </summary>
public class GetLeagueByIdQueryHandler(
    ILeagueDetailQuery detailQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueByIdQuery, LeagueDto>
{
    /// <summary>
    /// What the entry code reads as for a league that has none - anyone may join, so there is nothing to type in.
    /// </summary>
    private const string PublicEntryCode = "Public";


    public async Task<LeagueDto> Handle(GetLeagueByIdQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.Id, request.CurrentUserId, cancellationToken);

        var league = await detailQuery.ExecuteAsync(request.Id, cancellationToken);

        if (league is null)
            throw new EntityNotFoundException("League", request.Id);

        return new LeagueDto(
            league.Id,
            league.Name,
            league.SeasonName,
            league.ApprovedMemberCount,
            league.Price,
            league.EntryCode ?? PublicEntryCode,
            league.EntryDeadlineUtc,
            league.PointsForExactScore,
            league.PointsForCorrectResult,
            league.SeasonId,
            league.CompetitionType == CompetitionType.Tournament,
            league.HasPrizeScheme,
            league.RequiresMemberApproval,
            league.IsListed);
    }
}
