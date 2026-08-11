using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeaguePaymentInfoQuery"/> implementation must return.
///
/// This one guards a league's bank account, so the facts it reports have to be exactly right: whether the caller runs the
/// league, whether they have any membership of it at all, and the entry code as stored. Each of those is an input to the
/// authorisation rule, and an adapter that got one wrong would open or close the details to the wrong people.
/// </summary>
public abstract class LeaguePaymentInfoQueryConformanceTests
{
    protected abstract ILeaguePaymentInfoQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId + 5_000, world.AdministratorId, CancellationToken.None);

        // Assert
        row.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStillReturnTheLeague_ForACallerItHasNeverHeardOf()
    {
        // Arrange - the caller's name is joined in for the payment reference. An unknown caller must not make an existing
        // league look missing, which is what the old cross join did.
        var world = await ArrangeAsync();

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, "nobody-at-all", CancellationToken.None);

        // Assert - present, and refused by the handler rather than reported as not found.
        row.Should().NotBeNull();
        row!.IsAdministrator.Should().BeFalse();
        row.HasMembership.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportTheAdministrator()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        row!.IsAdministrator.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReportTheAdministratorOfAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueAsync(world.SeasonId, strangerId, "Their League");

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, strangerId, CancellationToken.None);

        // Assert
        row!.IsAdministrator.Should().BeFalse();
    }

    [Theory]
    [InlineData(LeagueMemberStatus.Approved)]
    [InlineData(LeagueMemberStatus.Pending)]
    [InlineData(LeagueMemberStatus.Rejected)]
    public async Task ExecuteAsync_ShouldReportAMembershipOfAnyStatus(LeagueMemberStatus status)
    {
        // Arrange
        var world = await ArrangeAsync();
        var memberId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, memberId, status);

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, memberId, CancellationToken.None);

        // Assert - faithful to the old EXISTS, which had no status filter. The pending case is the one that has to work:
        // somebody who has asked to join needs the bank details in order to pay.
        row!.HasMembership.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReportAMembershipOfAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.AdministratorId, "Other League");
        var memberId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(otherLeagueId, memberId);

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, memberId, CancellationToken.None);

        // Assert
        row!.HasMembership.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheCallersNameForTheirPaymentReference()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        row!.RequestingFirstName.Should().Be("Ada");
        row.RequestingLastName.Should().Be("Lovelace");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeaguesNameAndPrice()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        row!.LeagueName.Should().Be("Integration League");
        row.Price.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoEntryCodeAndNoBankDetails_ForALeagueWithoutThem()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var row = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - nulls, so the handler can tell "not set" from "set to something". A blank entry code must never match
        // a caller's blank one.
        row!.EntryCode.Should().BeNull();
        row.EncryptedAccountName.Should().BeNull();
        row.EncryptedSortCode.Should().BeNull();
        row.EncryptedAccountNumber.Should().BeNull();
        row.PaymentReferenceTemplate.Should().BeNull();
    }

    private async Task<PaymentWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new PaymentWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record PaymentWorld(int LeagueId, int SeasonId, string AdministratorId);
}
