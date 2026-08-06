using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Application.Features.Badges.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetBadgeLeaderboardQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetBadgeLeaderboardQuery, BadgeLeaderboardDto>
{
    public async Task<BadgeLeaderboardDto> Handle(GetBadgeLeaderboardQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS UserId,
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS DisplayName,
                COUNT(DISTINCT b.[BadgeKey]) AS BadgeCount,
                MAX(b.[AwardedUtc]) AS LastAwardedUtc
            FROM [AspNetUsers] u
            LEFT JOIN [UserBadges] b ON b.[UserId] = u.[Id]
            WHERE u.[FirstName] IS NOT NULL AND u.[FirstName] <> ''
            GROUP BY u.[Id], u.[FirstName], u.[LastName];";

        var rows = (await dbConnection.QueryAsync<LeaderboardRow>(sql, cancellationToken)).ToList();

        // Most badges first; ties broken by whoever reached their current tally earliest (their most recent
        // badge is the oldest); then by name. Players with no badges (null date) sort last.
        var ordered = rows
            .OrderByDescending(r => r.BadgeCount)
            .ThenBy(r => r.LastAwardedUtc ?? DateTime.MaxValue)
            .ThenBy(r => r.DisplayName)
            .ToList();

        var result = new List<BadgeLeaderboardRowDto>(ordered.Count);
        int? yourRank = null;

        for (var i = 0; i < ordered.Count; i++)
        {
            var row = ordered[i];
            var rank = i + 1;
            var isCurrentUser = row.UserId == request.UserId;

            if (isCurrentUser)
                yourRank = rank;

            result.Add(new BadgeLeaderboardRowDto(rank, row.UserId, row.DisplayName, row.BadgeCount,
                BadgeCatalogue.TotalBadgeCount, row.LastAwardedUtc, isCurrentUser));
        }

        return new BadgeLeaderboardDto(ordered.Count, yourRank, result);
    }

    private sealed record LeaderboardRow(string UserId, string DisplayName, int BadgeCount, DateTime? LastAwardedUtc);
}
