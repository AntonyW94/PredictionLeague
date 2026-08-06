using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

/// <summary>
/// Append-only record that a member has been sent the league welcome email (the post-deadline
/// email explaining members, prizes and boosts). Keyed on (<see cref="LeagueId"/>,
/// <see cref="UserId"/>) so the hourly scheduled task can re-scan safely without re-sending.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class LeagueWelcomeNotification
{
    [ExcludeFromCodeCoverage(Justification = "Set only by Dapper when hydrating from the database; the only constructor is private, so nothing else can reach it.")]
    public int Id { get; init; }
    public int LeagueId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public DateTime SentAtUtc { get; private set; }

    private LeagueWelcomeNotification() { }

    public static LeagueWelcomeNotification Create(int leagueId, string userId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NegativeOrZero(leagueId);
        Guard.Against.NullOrWhiteSpace(userId);

        return new LeagueWelcomeNotification
        {
            LeagueId = leagueId,
            UserId = userId,
            SentAtUtc = dateTimeProvider.UtcNow
        };
    }
}
