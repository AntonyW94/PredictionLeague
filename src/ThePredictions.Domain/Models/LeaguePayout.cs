using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Models;

/// <summary>
/// End-of-league settlement tracking for one winner: the aggregated total of their winnings in a league
/// plus a manual "paid" marker. Winnings remain the source of truth; this only tracks settlement state.
/// </summary>
public class LeaguePayout
{
    public int Id { get; init; }
    public int LeagueId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsPaid => PaidAtUtc.HasValue;

    [ExcludeFromCodeCoverage]
    private LeaguePayout() { }

    public LeaguePayout(int id, int leagueId, string userId, decimal totalAmount, DateTime? paidAtUtc, DateTime createdAtUtc, DateTime updatedAtUtc)
    {
        Id = id;
        LeagueId = leagueId;
        UserId = userId;
        TotalAmount = totalAmount;
        PaidAtUtc = paidAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static LeaguePayout Create(int leagueId, string userId, decimal totalAmount, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NegativeOrZero(leagueId);
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.Negative(totalAmount);

        var now = dateTimeProvider.UtcNow;

        return new LeaguePayout
        {
            LeagueId = leagueId,
            UserId = userId,
            TotalAmount = totalAmount,
            PaidAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Refreshes the total from the latest winnings. Deliberately a no-op once paid, so the amount that was
    /// actually paid stays frozen - a later correction is then surfaced as a discrepancy (live winnings vs this total).
    /// </summary>
    public void RefreshTotal(decimal newTotal, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.Negative(newTotal);

        if (IsPaid)
            return;

        TotalAmount = newTotal;
        UpdatedAtUtc = dateTimeProvider.UtcNow;
    }

    public void MarkPaid(IDateTimeProvider dateTimeProvider)
    {
        if (IsPaid)
            throw new BusinessRuleViolationException("This payout has already been marked as paid.");

        var now = dateTimeProvider.UtcNow;
        PaidAtUtc = now;
        UpdatedAtUtc = now;
    }
}
