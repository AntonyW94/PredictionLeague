using FluentAssertions;
using ThePredictions.Application.Features.Admin.Competitions.Queries;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What the administrator's reference-data reads must return: competitions, teams, the three settings tables, and the
/// stand-in player for an email preview.
///
/// Seven small ports in one suite because they promise the same thing - the rows, in no order, with nothing decided. The
/// orderings they used to apply were rules and now live in C#, so what these tests pin is that the read no longer
/// applies them.
/// </summary>
public abstract class AdminReferenceDataQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract ICompetitionsQuery Competitions { get; }

    protected abstract ITeamsQuery Teams { get; }

    protected abstract ISeasonTeamsQuery SeasonTeams { get; }

    protected abstract IPricingSettingsQuery PricingSettings { get; }

    protected abstract IRunningCostsQuery RunningCosts { get; }

    protected abstract IServiceFeesQuery ServiceFees { get; }

    protected abstract IEmailTestUserQuery EmailTestUser { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region Competitions

    [Fact]
    public async Task Competitions_ShouldReturnNothing_WhenThereAreNone()
    {
        (await Competitions.ExecuteAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Competitions_ShouldReturnEachCompetitionWithItsSeasonCount()
    {
        // Arrange - one competition with two seasons, and another with none.
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var emptyCompetitionId = await Seed.AddCompetitionAsync("EMPTY");

        // Act
        var competitions = await Competitions.ExecuteAsync(CancellationToken.None);

        // Assert
        competitions.Single(competition => competition.Id == backdrop.CompetitionId).SeasonCount.Should().Be(2);
        competitions.Single(competition => competition.Id == emptyCompetitionId).SeasonCount.Should().Be(0);
    }

    [Fact]
    public async Task Competitions_ShouldReturnEveryCompetitionRatherThanOneAtATime()
    {
        // Arrange - the single-competition screen picks one out of this set, so both had to arrive.
        await Seed.AddCompetitionAsync("ONE");
        await Seed.AddCompetitionAsync("TWO");

        // Act
        var competitions = await Competitions.ExecuteAsync(CancellationToken.None);

        // Assert
        competitions.Select(competition => competition.Code).Should().BeEquivalentTo(["ONE", "TWO"]);
    }

    #endregion

    #region Teams

    [Fact]
    public async Task Teams_ShouldReturnNothing_WhenThereAreNone()
    {
        (await Teams.ExecuteAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Teams_ShouldReturnEachTeamsDetails()
    {
        // Arrange
        var teamId = await Seed.AddTeamAsync("Arsenal", "ARS");

        // Act
        var team = (await Teams.ExecuteAsync(CancellationToken.None)).Single();

        // Assert
        team.Id.Should().Be(teamId);
        team.Name.Should().Be("Arsenal");
        team.ShortName.Should().NotBeNullOrWhiteSpace();
        team.Abbreviation.Should().Be("ARS");
    }

    [Fact]
    public async Task SeasonTeams_ShouldReturnOnlyTheTeamsPlayingInTheSeason()
    {
        // Arrange - two teams with a fixture between them, and a third team with none.
        var backdrop = await Seed.AddBackdropAsync();
        var unusedTeamId = await Seed.AddTeamAsync("Everton", "EVE");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        // Act
        var teams = await SeasonTeams.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert - which teams are in a season is not stored; it is worked out from the fixtures.
        teams.Select(team => team.Id).Should().BeEquivalentTo([backdrop.HomeTeamId, backdrop.AwayTeamId]);
        teams.Should().NotContain(team => team.Id == unusedTeamId);
    }

    [Fact]
    public async Task SeasonTeams_ShouldReturnATeamOnce_HoweverManyFixturesItHas()
    {
        // Arrange - the same pair playing twice, home and away.
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        // Act
        var teams = await SeasonTeams.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert - one row each. The statement this replaces needed a DISTINCT to undo its own join.
        teams.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeasonTeams_ShouldReturnNothing_ForASeasonWithNoFixtures()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var teams = await SeasonTeams.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        teams.Should().BeEmpty();
    }

    [Fact]
    public async Task SeasonTeams_ShouldNotReturnTeamsFromAnotherSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var otherRoundId = await Seed.AddRoundAsync(otherSeasonId, 1, Deadline);
        await Seed.AddMatchAsync(otherRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        // Act
        var teams = await SeasonTeams.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert
        teams.Should().BeEmpty();
    }

    #endregion

    #region Settings tables

    [Fact]
    public async Task PricingSettings_ShouldReturnNothing_WhenNoneHaveBeenSaved()
    {
        // The absent row is meaningful - it means the built-in defaults - and that is the handler's rule to apply.
        (await PricingSettings.ExecuteAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task PricingSettings_ShouldReturnTheSavedSettingsWithTheirId()
    {
        // Arrange
        var id = await Seed.AddPricingSettingsAsync(bufferRate: 0.15m, minimumFloor: 5m);

        // Act
        var settings = (await PricingSettings.ExecuteAsync(CancellationToken.None)).Single();

        // Assert - the id comes back because choosing between rows is a rule, and it was TOP 1 ORDER BY [Id] in SQL.
        settings.Id.Should().Be(id);
        settings.BufferRate.Should().Be(0.15m);
        settings.MinimumFloor.Should().Be(5m);
    }

    [Fact]
    public async Task RunningCosts_ShouldReturnNothing_WhenNoneHaveBeenRecorded()
    {
        (await RunningCosts.ExecuteAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunningCosts_ShouldReturnEachCostsDetails()
    {
        // Arrange
        var id = await Seed.AddRunningCostAsync("Hosting", 12.50m, "Monthly", Deadline, null, null);

        // Act
        var cost = (await RunningCosts.ExecuteAsync(CancellationToken.None)).Single();

        // Assert
        cost.Id.Should().Be(id);
        cost.Name.Should().Be("Hosting");
        cost.Amount.Should().Be(12.50m);
        cost.Frequency.Should().Be("Monthly");
        cost.StartDateUtc.Should().Be(Deadline);
        cost.EndDateUtc.Should().BeNull();
        cost.Notes.Should().BeNull();
    }

    [Fact]
    public async Task RunningCosts_ShouldReturnACostThatHasEndedAndOneWithNotes()
    {
        // Arrange
        await Seed.AddRunningCostAsync("Domain", 9m, "Yearly", Deadline, Deadline.AddYears(1), "Renews in January");

        // Act
        var cost = (await RunningCosts.ExecuteAsync(CancellationToken.None)).Single();

        // Assert
        cost.EndDateUtc.Should().Be(Deadline.AddYears(1));
        cost.Notes.Should().Be("Renews in January");
    }

    [Fact]
    public async Task ServiceFees_ShouldReturnNothing_WhenNoneHaveBeenRecorded()
    {
        (await ServiceFees.ExecuteAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceFees_ShouldReturnEachProvidersFees()
    {
        // Arrange
        await Seed.AddServiceFeeAsync("Stripe", percentFee: 1.5m, fixedFee: 0.20m);

        // Act
        var fee = (await ServiceFees.ExecuteAsync(CancellationToken.None)).Single();

        // Assert
        fee.Provider.Should().Be("Stripe");
        fee.PercentFee.Should().Be(1.5m);
        fee.FixedFee.Should().Be(0.20m);
    }

    #endregion

    #region The email preview's stand-in player

    [Fact]
    public async Task EmailTestUser_ShouldReturnNothing_ForAnIdThatMatchesNoPlayer()
    {
        // What to show instead - empty fields, so the preview still renders - is the handler's rule.
        (await EmailTestUser.ExecuteAsync("no-such-user", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task EmailTestUser_ShouldReturnBothNamePartsAndTheEmail()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var user = await EmailTestUser.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("Ada");
        user.LastName.Should().Be("Lovelace");
        user.Email.Should().NotBeNullOrWhiteSpace();
    }

    #endregion
}
