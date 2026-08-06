using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

/// <summary>
/// Log that a player has been sent an ad-hoc "you are missing predictions" reminder for a round.
/// Keyed on (<see cref="RoundId"/>, <see cref="UserId"/>) so the send throttle is per player per
/// round regardless of how many league owners trigger it - a player in several leagues is not
/// emailed repeatedly. <see cref="LastRemindedUtc"/> is refreshed on each send (upsert), so the
/// row also records the most recent nudge for the "reminded N hours ago" display.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PredictionReminderNotification
{
    [ExcludeFromCodeCoverage(Justification = "Set only by Dapper when hydrating from the database; the only constructor is private, so nothing else can reach it.")]
    public int Id { get; init; }
    public int RoundId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public DateTime LastRemindedUtc { get; private set; }
    public string RemindedByUserId { get; private set; } = string.Empty;

    private PredictionReminderNotification() { }

    public static PredictionReminderNotification Create(int roundId, string userId, string remindedByUserId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NegativeOrZero(roundId);
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NullOrWhiteSpace(remindedByUserId);

        return new PredictionReminderNotification
        {
            RoundId = roundId,
            UserId = userId,
            RemindedByUserId = remindedByUserId,
            LastRemindedUtc = dateTimeProvider.UtcNow
        };
    }
}
