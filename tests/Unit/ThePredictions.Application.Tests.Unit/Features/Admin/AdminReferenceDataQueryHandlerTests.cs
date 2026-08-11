using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Competitions.Queries;
using ThePredictions.Application.Features.Admin.EmailSettings.Queries;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin;

/// <summary>
/// The administrator's reference-data screens: competitions, teams, and the three settings tables.
///
/// Small handlers, and what they have in common is what used to be decided in SQL - an alphabetical order that deferred
/// to the database's collation, and, for the two single-row settings tables, which row counts as the live one.
/// </summary>
public class AdminReferenceDataQueryHandlerTests
{
    private static readonly DateTime StartDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    #region Competitions

    [Fact]
    public async Task FetchAllCompetitions_ShouldListThemAlphabetically()
    {
        // Arrange
        var query = Substitute.For<ICompetitionsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns([Competition(1, "Premier League"), Competition(2, "Champions League"), Competition(3, "FA Cup")]);

        // Act
        var competitions = await new FetchAllCompetitionsQueryHandler(query)
            .Handle(new FetchAllCompetitionsQuery(), CancellationToken.None);

        // Assert
        competitions.Select(competition => competition.Name)
            .Should().Equal("Champions League", "FA Cup", "Premier League");
    }

    [Fact]
    public async Task FetchAllCompetitions_ShouldReportEachCompetitionsDetails()
    {
        // Arrange
        var query = Substitute.For<ICompetitionsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([Competition(7, "Premier League") with
        {
            Code = "PL",
            Type = 0,
            LogoUrl = "pl.png",
            Description = "The big one",
            ApiLeagueId = 39,
            SeasonCount = 3
        }]);

        // Act
        var competition = (await new FetchAllCompetitionsQueryHandler(query)
            .Handle(new FetchAllCompetitionsQuery(), CancellationToken.None)).Single();

        // Assert
        competition.Id.Should().Be(7);
        competition.Code.Should().Be("PL");
        competition.Name.Should().Be("Premier League");
        competition.Type.Should().Be(0);
        competition.LogoUrl.Should().Be("pl.png");
        competition.Description.Should().Be("The big one");
        competition.ApiLeagueId.Should().Be(39);
        competition.SeasonCount.Should().Be(3);
    }

    [Fact]
    public async Task FetchAllCompetitions_ShouldReturnNothing_WhenThereAreNone()
    {
        // Arrange
        var query = Substitute.For<ICompetitionsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var competitions = await new FetchAllCompetitionsQueryHandler(query)
            .Handle(new FetchAllCompetitionsQuery(), CancellationToken.None);

        // Assert
        competitions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompetitionById_ShouldReturnTheOneAskedFor()
    {
        // Arrange
        var query = Substitute.For<ICompetitionsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns([Competition(1, "Premier League"), Competition(2, "FA Cup")]);

        // Act
        var competition = await new GetCompetitionByIdQueryHandler(query)
            .Handle(new GetCompetitionByIdQuery(2), CancellationToken.None);

        // Assert
        competition.Name.Should().Be("FA Cup");
    }

    [Fact]
    public async Task GetCompetitionById_ShouldReportNotFound_WhenThereIsNoSuchCompetition()
    {
        // Arrange
        var query = Substitute.For<ICompetitionsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([Competition(1, "Premier League")]);

        // Act
        var act = () => new GetCompetitionByIdQueryHandler(query)
            .Handle(new GetCompetitionByIdQuery(99), CancellationToken.None);

        // Assert - a client asking for something that does not exist is a 404, not a server fault.
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region Teams

    [Fact]
    public async Task FetchAllTeams_ShouldListEveryTeamAlphabetically_WhenNoSeasonIsAskedFor()
    {
        // Arrange
        var teams = Substitute.For<ITeamsQuery>();
        var seasonTeams = Substitute.For<ISeasonTeamsQuery>();
        teams.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([Team(1, "Wolves"), Team(2, "Arsenal")]);

        // Act
        var result = await new FetchAllTeamsQueryHandler(teams, seasonTeams)
            .Handle(new FetchAllTeamsQuery(null), CancellationToken.None);

        // Assert
        result.Select(team => team.Name).Should().Equal("Arsenal", "Wolves");
        await seasonTeams.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task FetchAllTeams_ShouldListOnlyTheSeasonsTeams_WhenASeasonIsAskedFor()
    {
        // Arrange
        var teams = Substitute.For<ITeamsQuery>();
        var seasonTeams = Substitute.For<ISeasonTeamsQuery>();
        seasonTeams.ExecuteAsync(7, Arg.Any<CancellationToken>()).Returns([Team(3, "Chelsea")]);

        // Act
        var result = await new FetchAllTeamsQueryHandler(teams, seasonTeams)
            .Handle(new FetchAllTeamsQuery(7), CancellationToken.None);

        // Assert
        result.Select(team => team.Name).Should().Equal("Chelsea");
        await teams.DidNotReceiveWithAnyArgs().ExecuteAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FetchAllTeams_ShouldReportATeamWithNoBadge()
    {
        // A team added by hand before its badge has been found. Two of the three reads of this table used to declare the
        // column as never-null, which put a null into a field that said it could not hold one.
        var teams = Substitute.For<ITeamsQuery>();
        teams.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([Team(1, "Arsenal") with { LogoUrl = null }]);

        // Act
        var result = await new FetchAllTeamsQueryHandler(teams, Substitute.For<ISeasonTeamsQuery>())
            .Handle(new FetchAllTeamsQuery(null), CancellationToken.None);

        // Assert
        result.Single().LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetTeamById_ShouldReturnTheOneAskedForWithItsDetails()
    {
        // Arrange
        var teams = Substitute.For<ITeamsQuery>();
        teams.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([Team(1, "Arsenal"), Team(2, "Chelsea")]);

        // Act
        var team = await new GetTeamByIdQueryHandler(teams).Handle(new GetTeamByIdQuery(2), CancellationToken.None);

        // Assert
        team.Id.Should().Be(2);
        team.Name.Should().Be("Chelsea");
        team.ShortName.Should().Be("Chelsea");
        team.Abbreviation.Should().Be("CHE");
        team.LogoUrl.Should().Be("chelsea.png");
        team.ApiTeamId.Should().Be(42);
    }

    [Fact]
    public async Task GetTeamById_ShouldReportNotFound_WhenThereIsNoSuchTeam()
    {
        // Arrange
        var teams = Substitute.For<ITeamsQuery>();
        teams.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([Team(1, "Arsenal")]);

        // Act
        var act = () => new GetTeamByIdQueryHandler(teams).Handle(new GetTeamByIdQuery(99), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region Pricing settings

    [Fact]
    public async Task GetPricingSettings_ShouldReturnTheSavedSettings()
    {
        // Arrange
        var query = Substitute.For<IPricingSettingsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([new PricingSettingsRow(1, 0.15m, 5m)]);

        // Act
        var settings = await new GetPricingSettingsQueryHandler(query)
            .Handle(new GetPricingSettingsQuery(), CancellationToken.None);

        // Assert
        settings.BufferRate.Should().Be(0.15m);
        settings.MinimumFloor.Should().Be(5m);
    }

    [Fact]
    public async Task GetPricingSettings_ShouldFallBackToTheBuiltInDefaults_WhenNothingHasBeenSaved()
    {
        // Arrange
        var query = Substitute.For<IPricingSettingsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var settings = await new GetPricingSettingsQueryHandler(query)
            .Handle(new GetPricingSettingsQuery(), CancellationToken.None);

        // Assert - the screen renders with the defaults rather than failing on a table nobody has filled in.
        settings.BufferRate.Should().BePositive();
        settings.MinimumFloor.Should().BePositive();
    }

    [Fact]
    public async Task GetPricingSettings_ShouldTreatTheEarliestRowAsTheLiveOne()
    {
        // This is a single-row table by convention rather than by constraint, so which row wins if a second appears is a
        // decision. It was TOP 1 ORDER BY [Id] in SQL, and the rows deliberately arrive out of order here.
        var query = Substitute.For<IPricingSettingsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns([new PricingSettingsRow(9, 0.99m, 99m), new PricingSettingsRow(2, 0.15m, 5m)]);

        // Act
        var settings = await new GetPricingSettingsQueryHandler(query)
            .Handle(new GetPricingSettingsQuery(), CancellationToken.None);

        // Assert
        settings.BufferRate.Should().Be(0.15m);
    }

    #endregion

    #region Running costs and service fees

    [Fact]
    public async Task GetRunningCosts_ShouldListThemAlphabeticallyWithTheirDetails()
    {
        // Arrange
        var query = Substitute.For<IRunningCostsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns([
                new RunningCostRow(1, "Hosting", 12m, "Monthly", StartDate, null, null),
                new RunningCostRow(2, "Domain", 9m, "Yearly", StartDate, StartDate.AddYears(1), "Renews in January")
            ]);

        // Act
        var costs = (await new GetRunningCostsQueryHandler(query)
            .Handle(new GetRunningCostsQuery(), CancellationToken.None)).ToList();

        // Assert
        costs.Select(cost => cost.Name).Should().Equal("Domain", "Hosting");
        costs[0].Amount.Should().Be(9m);
        costs[0].Frequency.Should().Be("Yearly");
        costs[0].StartDateUtc.Should().Be(StartDate);
        costs[0].EndDateUtc.Should().Be(StartDate.AddYears(1));
        costs[0].Notes.Should().Be("Renews in January");
        costs[1].EndDateUtc.Should().BeNull();
        costs[1].Notes.Should().BeNull();
    }

    [Fact]
    public async Task GetRunningCosts_ShouldReturnNothing_WhenNoneHaveBeenRecorded()
    {
        // Arrange
        var query = Substitute.For<IRunningCostsQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var costs = await new GetRunningCostsQueryHandler(query).Handle(new GetRunningCostsQuery(), CancellationToken.None);

        // Assert
        costs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetServiceFees_ShouldListThemAlphabeticallyByProvider()
    {
        // Arrange
        var query = Substitute.For<IServiceFeesQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>())
            // The fees deliberately run the other way to the names, so ordering by whichever number came to hand fails.
            .Returns([new ServiceFeeRow("Stripe", 1.5m, 0.20m), new ServiceFeeRow("Brevo", 0m, 5m)]);

        // Act
        var fees = (await new GetServiceFeesQueryHandler(query)
            .Handle(new GetServiceFeesQuery(), CancellationToken.None)).ToList();

        // Assert
        fees.Select(fee => fee.Provider).Should().Equal("Brevo", "Stripe");
        fees[0].PercentFee.Should().Be(0m);
        fees[0].FixedFee.Should().Be(5m);
        fees[1].PercentFee.Should().Be(1.5m);
        fees[1].FixedFee.Should().Be(0.20m);
    }

    [Fact]
    public async Task GetServiceFees_ShouldReturnNothing_WhenNoneHaveBeenRecorded()
    {
        // Arrange
        var query = Substitute.For<IServiceFeesQuery>();
        query.ExecuteAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var fees = await new GetServiceFeesQueryHandler(query).Handle(new GetServiceFeesQuery(), CancellationToken.None);

        // Assert
        fees.Should().BeEmpty();
    }

    #endregion

    #region Email settings

    [Fact]
    public async Task GetEmailSettings_ShouldReportTheSavedSwitch()
    {
        // Arrange
        var query = Substitute.For<IEmailSettingsQuery>();
        query.GetEmailsEnabledAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var settings = await new GetEmailSettingsQueryHandler(query)
            .Handle(new GetEmailSettingsQuery(), CancellationToken.None);

        // Assert
        settings.EmailsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetEmailSettings_ShouldReportEmailsOn_WhenNothingHasBeenSaved()
    {
        // The same rule the sending path applies to the same absent row. This handler used to read the switch with its
        // own copy of the identical statement.
        var query = Substitute.For<IEmailSettingsQuery>();
        query.GetEmailsEnabledAsync(Arg.Any<CancellationToken>()).Returns((bool?)null);

        // Act
        var settings = await new GetEmailSettingsQueryHandler(query)
            .Handle(new GetEmailSettingsQuery(), CancellationToken.None);

        // Assert
        settings.EmailsEnabled.Should().BeTrue();
    }

    #endregion

    private static CompetitionRow Competition(int id, string name) =>
        new(id, $"C{id}", name, Type: 0, LogoUrl: null, Description: null, ApiLeagueId: null, SeasonCount: 1);

    private static TeamRow Team(int id, string name) =>
        new(id, name, name, $"{name.ToLowerInvariant()}.png", name[..3].ToUpperInvariant(), ApiTeamId: 42);
}
