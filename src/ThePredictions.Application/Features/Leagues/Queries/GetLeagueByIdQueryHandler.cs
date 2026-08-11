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

    /// <summary>
    /// What a league with no entry deadline reports instead of one.
    /// </summary>
    /// <remarks>
    /// A sentinel, and not a good one, but it is the one that is already on screen: the contract's
    /// <c>EntryDeadlineUtc</c> is not nullable, so the old statement's <c>ISNULL(..., '1900-01-01')</c> is what a league
    /// with no deadline currently shows. Named here rather than left as a bare literal so the next reader can see that
    /// it means "never" rather than a real date. Making the contract nullable would be the honest fix and would ripple
    /// through the pages that format it, so it is recorded in the plan document instead.
    /// </remarks>
    private static readonly DateTime NoEntryDeadline = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
            league.TotalMembershipCount,
            league.Price,
            league.EntryCode ?? PublicEntryCode,
            league.EntryDeadlineUtc ?? NoEntryDeadline,
            league.PointsForExactScore,
            league.PointsForCorrectResult,
            league.SeasonId,
            league.CompetitionType == CompetitionType.Tournament,
            league.HasPrizeScheme,
            league.RequiresMemberApproval,
            league.IsListed);
    }
}
