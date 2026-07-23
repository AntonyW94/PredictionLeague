using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;

namespace ThePredictions.Domain.Models;

/// <summary>
/// One badge a user has earned (Achievements & Badges feature). All badges are global - earned once,
/// in any league. <see cref="AwardedUtc"/> is the real achievement date, which is why the factory takes
/// it explicitly rather than reading the clock: live awards pass "now" (the round just completed) and
/// retrospective awards pass the historical event date. <see cref="LeagueId"/> is provenance for the
/// caption; <see cref="RoundId"/>/<see cref="SeasonId"/> scope the repeatable badges.
/// </summary>
public class AwardedBadge
{
    public int Id { get; init; }
    public string UserId { get; private set; } = string.Empty;
    public string BadgeKey { get; private set; } = string.Empty;
    public DateTime AwardedUtc { get; private set; }
    public int? LeagueId { get; private set; }
    public int? RoundId { get; private set; }
    public int? SeasonId { get; private set; }
    public string? Detail { get; private set; }

    [ExcludeFromCodeCoverage]
    private AwardedBadge() { }

    public AwardedBadge(int id, string userId, string badgeKey, DateTime awardedUtc, int? leagueId, int? roundId, int? seasonId, string? detail)
    {
        Id = id;
        UserId = userId;
        BadgeKey = badgeKey;
        AwardedUtc = awardedUtc;
        LeagueId = leagueId;
        RoundId = roundId;
        SeasonId = seasonId;
        Detail = detail;
    }

    public static AwardedBadge Create(string userId, string badgeKey, DateTime awardedUtc, int? leagueId = null, int? roundId = null, int? seasonId = null, string? detail = null)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NullOrWhiteSpace(badgeKey);
        Guard.Against.Default(awardedUtc);

        if (leagueId.HasValue)
            Guard.Against.NegativeOrZero(leagueId.Value);

        if (roundId.HasValue)
            Guard.Against.NegativeOrZero(roundId.Value);

        if (seasonId.HasValue)
            Guard.Against.NegativeOrZero(seasonId.Value);

        return new AwardedBadge
        {
            UserId = userId,
            BadgeKey = badgeKey,
            AwardedUtc = awardedUtc,
            LeagueId = leagueId,
            RoundId = roundId,
            SeasonId = seasonId,
            Detail = detail
        };
    }
}
