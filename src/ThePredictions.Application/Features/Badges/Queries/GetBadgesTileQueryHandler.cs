using MediatR;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>
/// The dashboard tile: the collection carousel, plus where the player stands against everyone else.
/// </summary>
public class GetBadgesTileQueryHandler(
    IBadgeStateQuery badgeStateQuery,
    IBadgeLeaderboardQuery badgeLeaderboardQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetBadgesTileQuery, BadgesTileDto>
{
    public async Task<BadgesTileDto> Handle(GetBadgesTileQuery request, CancellationToken cancellationToken)
    {
        var state = await badgeStateQuery.ExecuteAsync(request.UserId, cancellationToken);
        var tile = BadgeCatalogue.BuildTile(BadgeState.From(state), dateTimeProvider.UtcNow);

        // The same table the badges page shows, so the position on the tile is the position they will see when they
        // tap it. This used to be a second statement that worked the rank out its own way and could disagree.
        var standings = BadgeStandings.Of(await badgeLeaderboardQuery.ExecuteAsync(cancellationToken));

        // Nothing to show a player who is not on the table - an account with no name yet - rather than the first
        // place the old statement handed them for having nobody ahead of them.
        var theirs = standings.FirstOrDefault(standing => standing.Item.UserId == request.UserId);

        return tile with { YourRank = theirs?.Rank, TotalPlayers = standings.Count };
    }
}
