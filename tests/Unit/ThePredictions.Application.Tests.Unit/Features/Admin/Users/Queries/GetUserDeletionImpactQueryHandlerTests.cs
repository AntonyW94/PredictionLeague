using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Queries;

/// <summary>
/// What the delete confirmation dialog is told before it asks an administrator to commit.
/// </summary>
public class GetUserDeletionImpactQueryHandlerTests
{
    private const string UserId = "user-1";

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IUserDeletionImpactQuery _impactQuery = Substitute.For<IUserDeletionImpactQuery>();

    private readonly GetUserDeletionImpactQueryHandler _handler;

    public GetUserDeletionImpactQueryHandlerTests()
    {
        _handler = new GetUserDeletionImpactQueryHandler(_userManager, _impactQuery);

        _userManager.FindByIdAsync(UserId).Returns(User(UserId));
        _impactQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Row());
    }

    private static ApplicationUser User(string id) =>
        new() { Id = id, Email = $"{id}@example.com", FirstName = "Alice", LastName = "Anderson" };

    private static UserDeletionImpactRow Row(
        int seasonPasses = 0,
        decimal seasonPassSpend = 0m,
        int leagueMemberships = 0,
        int predictions = 0,
        int winnings = 0,
        decimal winningsTotal = 0m,
        int payouts = 0,
        decimal payoutsTotal = 0m,
        int badges = 0,
        int boostUsages = 0,
        int roundResults = 0,
        int leagueRoundResults = 0,
        int leagueStandings = 0,
        int emailRecords = 0,
        int onboardingSkips = 0,
        bool hasPayoutDetails = false,
        int leaguesAdministered = 0) =>
        new(seasonPasses, seasonPassSpend, leagueMemberships, predictions, winnings, winningsTotal, payouts,
            payoutsTotal, badges, boostUsages, roundResults, leagueRoundResults, leagueStandings, emailRecords,
            onboardingSkips, hasPayoutDetails, leaguesAdministered);

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheAccountDoesNotExist()
    {
        // Without the lookup the read would happily return a row of zeroes for an id that never existed, and
        // the dialog would offer to delete nothing at all.
        _userManager.FindByIdAsync("missing").Returns((ApplicationUser?)null);

        var handling = async () => await _handler.Handle(new GetUserDeletionImpactQuery("missing"), CancellationToken.None);

        await handling.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldNotRunTheRead_WhenTheAccountDoesNotExist()
    {
        _userManager.FindByIdAsync("missing").Returns((ApplicationUser?)null);

        var handling = async () => await _handler.Handle(new GetUserDeletionImpactQuery("missing"), CancellationToken.None);

        await handling.Should().ThrowAsync<EntityNotFoundException>();

        await _impactQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReadTheImpactForTheRequestedAccount()
    {
        await _handler.Handle(new GetUserDeletionImpactQuery(UserId), CancellationToken.None);

        await _impactQuery.Received(1).ExecuteAsync(UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCarryEveryCountThrough()
    {
        // Seventeen values mapped by position across two record types, which is precisely the mapping a
        // reorder breaks silently. Distinct values per field, so a transposed pair fails rather than passes.
        _impactQuery.ExecuteAsync(UserId, Arg.Any<CancellationToken>()).Returns(Row(
            seasonPasses: 1,
            seasonPassSpend: 25m,
            leagueMemberships: 2,
            predictions: 3,
            winnings: 4,
            winningsTotal: 47.25m,
            payouts: 5,
            payoutsTotal: 30m,
            badges: 6,
            boostUsages: 7,
            roundResults: 8,
            leagueRoundResults: 9,
            leagueStandings: 10,
            emailRecords: 11,
            onboardingSkips: 12,
            hasPayoutDetails: true,
            leaguesAdministered: 13));

        var impact = await _handler.Handle(new GetUserDeletionImpactQuery(UserId), CancellationToken.None);

        impact.SeasonPasses.Should().Be(1);
        impact.SeasonPassSpend.Should().Be(25m);
        impact.LeagueMemberships.Should().Be(2);
        impact.Predictions.Should().Be(3);
        impact.Winnings.Should().Be(4);
        impact.WinningsTotal.Should().Be(47.25m);
        impact.Payouts.Should().Be(5);
        impact.PayoutsTotal.Should().Be(30m);
        impact.Badges.Should().Be(6);
        impact.BoostUsages.Should().Be(7);
        impact.RoundResults.Should().Be(8);
        impact.LeagueRoundResults.Should().Be(9);
        impact.LeagueStandings.Should().Be(10);
        impact.EmailRecords.Should().Be(11);
        impact.OnboardingSkips.Should().Be(12);
        impact.HasPayoutDetails.Should().BeTrue();
        impact.LeaguesAdministered.Should().Be(13);
    }

    [Fact]
    public async Task Handle_ShouldReportNothing_ForAnAccountThatHasOnlyEverRegistered()
    {
        var impact = await _handler.Handle(new GetUserDeletionImpactQuery(UserId), CancellationToken.None);

        impact.HasAnyRecords.Should().BeFalse();
        impact.HasFinancialRecords.Should().BeFalse();
    }
}
