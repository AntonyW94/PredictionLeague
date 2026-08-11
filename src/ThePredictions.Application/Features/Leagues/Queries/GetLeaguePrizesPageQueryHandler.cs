using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's prize page: what the pot is worth and how it is divided up.
/// </summary>
public class GetLeaguePrizesPageQueryHandler(
    ILeaguePrizesPageQuery prizesQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeaguePrizesPageQuery, LeaguePrizesPageDto>
{
    /// <summary>
    /// What a league with no entry deadline reports instead of one - the same sentinel the league settings page uses,
    /// for the same reason: the contract's property is not nullable. See <see cref="GetLeagueByIdQueryHandler"/>.
    /// </summary>
    private static readonly DateTime NoEntryDeadline = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<LeaguePrizesPageDto> Handle(
        GetLeaguePrizesPageQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await prizesQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (data is null)
            throw new EntityNotFoundException("League", request.LeagueId);

        var header = data.Header;

        return new LeaguePrizesPageDto
        {
            LeagueName = header.LeagueName,
            EntryDeadlineUtc = header.EntryDeadlineUtc ?? NoEntryDeadline,
            Price = header.Price,
            MemberCount = header.TotalMembershipCount,
            NumberOfRounds = header.NumberOfRounds,
            SeasonStartDateUtc = header.SeasonStartDateUtc,
            SeasonEndDateUtc = header.SeasonEndDateUtc,
            PrizeSettings = data.PrizeSettings
                .Select(prize => new PrizeSettingDto(prize.PrizeType, prize.Rank, prize.PrizeAmount, prize.Stage))
                .ToList()
        };
    }
}
