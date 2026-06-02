using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeaguePrizeSchemeOnLeagueTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));

    private Season CreateFutureSeason() =>
        new(id: 1, name: "2026/27", startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8), isActive: true, numberOfRounds: 38, competitionId: 1,
            passStandardPrice: null, passPremiumPrice: null);

    private League CreateLeague(decimal price) =>
        League.Create(1, "Test League", "admin-user", _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, price, CreateFutureSeason(), _dateTimeProvider);

    private LeaguePrizeScheme CreateScheme(int stake, int topUp = 0) =>
        LeaguePrizeScheme.Create(stake, topUp, 100, new[] { LeaguePrizeSchemeEntry.Create(PrizeType.Overall, stake) }, "admin-user", false, _dateTimeProvider);

    [Fact]
    public void SetPrizeScheme_ShouldAttachScheme_AndFlagPrizes_WhenPaidLeague()
    {
        var league = CreateLeague(10m);
        var scheme = CreateScheme(10);

        league.SetPrizeScheme(scheme);

        league.PrizeScheme.Should().BeSameAs(scheme);
        league.HasPrizes.Should().BeTrue();
    }

    [Fact]
    public void SetPrizeScheme_ShouldNotFlagPrizes_WhenFreeLeagueWithoutTopUp()
    {
        var league = CreateLeague(0m);
        var scheme = CreateScheme(0);

        league.SetPrizeScheme(scheme);

        league.HasPrizes.Should().BeFalse();
    }

    [Fact]
    public void SetPrizeScheme_ShouldFlagPrizes_WhenFreeLeagueHasAdminTopUp()
    {
        var league = CreateLeague(0m);
        var scheme = CreateScheme(0, topUp: 50);

        league.SetPrizeScheme(scheme);

        league.HasPrizes.Should().BeTrue();
    }

    [Fact]
    public void SetPrizeScheme_ShouldThrow_WhenAlreadySet()
    {
        var league = CreateLeague(10m);
        league.SetPrizeScheme(CreateScheme(10));

        var act = () => league.SetPrizeScheme(CreateScheme(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetPrizeScheme_ShouldThrow_WhenSchemeNull()
    {
        var league = CreateLeague(10m);
        var act = () => league.SetPrizeScheme(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OverridePrizeScheme_ShouldReplaceScheme_EvenWhenAlreadySet()
    {
        var league = CreateLeague(10m);
        var original = CreateScheme(10);
        var replacement = CreateScheme(10);
        league.SetPrizeScheme(original);

        league.OverridePrizeScheme(replacement);

        league.PrizeScheme.Should().BeSameAs(replacement);
        league.HasPrizes.Should().BeTrue();
    }

    [Fact]
    public void OverridePrizeScheme_ShouldThrow_WhenSchemeNull()
    {
        var league = CreateLeague(10m);
        var act = () => league.OverridePrizeScheme(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HydrationConstructor_ShouldExposePrizeScheme()
    {
        var scheme = CreateScheme(10);
        var league = new League(
            id: 1, name: "Test League", seasonId: 1, administratorUserId: "admin-user", entryCode: "ABC123",
            createdAtUtc: _dateTimeProvider.UtcNow, entryDeadlineUtc: _dateTimeProvider.UtcNow.AddMonths(1),
            pointsForExactScore: 3, pointsForCorrectResult: 1, price: 10m, isFree: false, hasPrizes: true,
            prizeFundOverride: null, members: null, prizeSettings: null, prizeScheme: scheme);

        league.PrizeScheme.Should().BeSameAs(scheme);
    }
}
