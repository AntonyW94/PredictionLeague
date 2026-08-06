using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Badges.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetBadgesTileQueryHandler(IApplicationReadDbConnection dbConnection, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetBadgesTileQuery, BadgesTileDto>
{
    public async Task<BadgesTileDto> Handle(GetBadgesTileQuery request, CancellationToken cancellationToken)
    {
        var state = await BadgeStateQueries.LoadAsync(dbConnection, request.UserId, cancellationToken);
        var tile = BadgeCatalogue.BuildTile(state, dateTimeProvider.UtcNow);

        // The player's standing on the site-wide badges leaderboard, for the competitive nudge on the tile.
        // Rank = 1 + players ahead (more badges, or level on badges but reached their tally earlier).
        const string rankSql = @"
            WITH Counts AS (
                SELECT u.[Id] AS UserId, COUNT(DISTINCT b.[BadgeKey]) AS Cnt, MAX(b.[AwardedUtc]) AS LastAwarded
                FROM [AspNetUsers] u
                LEFT JOIN [UserBadges] b ON b.[UserId] = u.[Id]
                WHERE u.[FirstName] IS NOT NULL AND u.[FirstName] <> ''
                GROUP BY u.[Id]
            ),
            Me AS (SELECT Cnt, LastAwarded FROM Counts WHERE UserId = @UserId)
            SELECT
                (SELECT COUNT(*) FROM Counts) AS TotalPlayers,
                (SELECT COUNT(*) + 1
                 FROM Counts c CROSS JOIN Me
                 WHERE c.Cnt > Me.Cnt OR (c.Cnt = Me.Cnt AND c.LastAwarded < Me.LastAwarded)) AS YourRank;";

        var standing = await dbConnection.QuerySingleOrDefaultAsync<StandingRow>(rankSql, cancellationToken, new { request.UserId });

        return standing is null
            ? tile
            : tile with { YourRank = standing.YourRank, TotalPlayers = standing.TotalPlayers };
    }

    private sealed record StandingRow(int TotalPlayers, int YourRank);
}
