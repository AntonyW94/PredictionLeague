using MediatR;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>The site-wide badges table, with this player's own row marked.</summary>
public class GetBadgeLeaderboardQueryHandler(IBadgeLeaderboardQuery badgeLeaderboardQuery)
    : IRequestHandler<GetBadgeLeaderboardQuery, BadgeLeaderboardDto>
{
    public async Task<BadgeLeaderboardDto> Handle(GetBadgeLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var standings = BadgeStandings.Of(await badgeLeaderboardQuery.ExecuteAsync(cancellationToken));

        var rows = standings
            .Select(standing => ToRowDto(standing, request.UserId))
            .ToList();

        var yourRank = standings.FirstOrDefault(standing => standing.Item.UserId == request.UserId)?.Rank;

        return new BadgeLeaderboardDto(standings.Count, yourRank, rows);
    }

    private static BadgeLeaderboardRowDto ToRowDto(Ranked<BadgeStanding> standing, string currentUserId) =>
        new(standing.Rank,
            standing.Item.UserId,
            standing.Item.DisplayName,
            standing.Item.Tally.BadgeCount,
            BadgeCatalogue.TotalBadgeCount,
            standing.Item.Tally.LastAwardedUtc,
            standing.Item.UserId == currentUserId);
}
