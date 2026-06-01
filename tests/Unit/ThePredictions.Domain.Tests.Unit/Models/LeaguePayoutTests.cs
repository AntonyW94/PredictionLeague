using FluentAssertions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeaguePayoutTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_ShouldSetPropertiesAndBeUnpaid()
    {
        var payout = LeaguePayout.Create(5, "user-1", 40m, _dateTimeProvider);

        payout.LeagueId.Should().Be(5);
        payout.UserId.Should().Be("user-1");
        payout.TotalAmount.Should().Be(40m);
        payout.PaidAtUtc.Should().BeNull();
        payout.IsPaid.Should().BeFalse();
        payout.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        payout.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ShouldThrow_WhenLeagueIdNotPositive(int leagueId)
    {
        var act = () => LeaguePayout.Create(leagueId, "user-1", 40m, _dateTimeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenUserIdBlank()
    {
        var act = () => LeaguePayout.Create(5, " ", 40m, _dateTimeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenTotalNegative()
    {
        var act = () => LeaguePayout.Create(5, "user-1", -1m, _dateTimeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPaid_ShouldSetPaidAtAndIsPaid()
    {
        var payout = LeaguePayout.Create(5, "user-1", 40m, _dateTimeProvider);
        _dateTimeProvider.AdvanceBy(TimeSpan.FromDays(3));

        payout.MarkPaid(_dateTimeProvider);

        payout.IsPaid.Should().BeTrue();
        payout.PaidAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        payout.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void MarkPaid_ShouldThrow_WhenAlreadyPaid()
    {
        var payout = LeaguePayout.Create(5, "user-1", 40m, _dateTimeProvider);
        payout.MarkPaid(_dateTimeProvider);

        var act = () => payout.MarkPaid(_dateTimeProvider);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RefreshTotal_ShouldUpdateTotal_WhenNotPaid()
    {
        var payout = LeaguePayout.Create(5, "user-1", 40m, _dateTimeProvider);
        _dateTimeProvider.AdvanceBy(TimeSpan.FromDays(1));

        payout.RefreshTotal(55m, _dateTimeProvider);

        payout.TotalAmount.Should().Be(55m);
        payout.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void RefreshTotal_ShouldBeNoOp_WhenAlreadyPaid()
    {
        var payout = LeaguePayout.Create(5, "user-1", 40m, _dateTimeProvider);
        payout.MarkPaid(_dateTimeProvider);

        payout.RefreshTotal(99m, _dateTimeProvider);

        // Frozen once paid, so a later correction is surfaced as a discrepancy rather than overwriting.
        payout.TotalAmount.Should().Be(40m);
    }

    [Fact]
    public void RefreshTotal_ShouldThrow_WhenNegative()
    {
        var payout = LeaguePayout.Create(5, "user-1", 40m, _dateTimeProvider);

        var act = () => payout.RefreshTotal(-5m, _dateTimeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabaseValues()
    {
        var paidAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var payout = new LeaguePayout(7, 5, "user-1", 40m, paidAt, created, updated);

        payout.Id.Should().Be(7);
        payout.LeagueId.Should().Be(5);
        payout.UserId.Should().Be("user-1");
        payout.TotalAmount.Should().Be(40m);
        payout.PaidAtUtc.Should().Be(paidAt);
        payout.IsPaid.Should().BeTrue();
        payout.CreatedAtUtc.Should().Be(created);
        payout.UpdatedAtUtc.Should().Be(updated);
    }
}
