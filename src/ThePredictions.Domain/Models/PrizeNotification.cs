using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

/// <summary>
/// Append-only record that a winner has been told about a specific prize, used to make prize
/// notifications idempotent. <see cref="Winning"/> rows are deleted and re-created every time a
/// round is re-processed, so they cannot carry a "notified" flag; this log persists across
/// re-processing and is keyed on the winning's stable identity
/// (<see cref="UserId"/>, <see cref="LeaguePrizeSettingId"/>, <see cref="RoundNumber"/>,
/// <see cref="Month"/>).
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PrizeNotification
{
    [ExcludeFromCodeCoverage(Justification = "Set only by Dapper when hydrating from the database; the only constructor is private, so nothing else can reach it.")]
    public int Id { get; init; }
    public string UserId { get; private set; } = string.Empty;
    public int LeaguePrizeSettingId { get; private set; }
    public int? RoundNumber { get; private set; }
    public int? Month { get; private set; }
    public DateTime SentAtUtc { get; private set; }

    private PrizeNotification() { }

    public static PrizeNotification Create(string userId, int leaguePrizeSettingId, int? roundNumber, int? month, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(leaguePrizeSettingId);

        return new PrizeNotification
        {
            UserId = userId,
            LeaguePrizeSettingId = leaguePrizeSettingId,
            RoundNumber = roundNumber,
            Month = month,
            SentAtUtc = dateTimeProvider.UtcNow
        };
    }
}
