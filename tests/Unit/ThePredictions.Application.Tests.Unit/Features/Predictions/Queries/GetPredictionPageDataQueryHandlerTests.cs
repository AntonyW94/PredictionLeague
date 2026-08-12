using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Predictions.Queries;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Predictions;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Predictions.Queries;

/// <summary>
/// The prediction form for a round.
///
/// Most of these tests are about the boost state shown against each league, which was two nested <c>EXISTS</c> blocks - one
/// asking whether the league runs boosts at all and one, with a <c>NOT EXISTS</c> inside it, asking whether this player has
/// one left for the season.
/// </summary>
public class GetPredictionPageDataQueryHandlerTests
{
    private const string UserId = "user-me";
    private const int RoundId = 42;
    private const int SeasonId = 7;
    private const int LeagueId = 100;
    private const int DoublePointsId = 1;
    private const int BankerId = 2;

    private static readonly DateTime KickOff = new(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly IRoundHeaderQuery _roundHeaderQuery = Substitute.For<IRoundHeaderQuery>();
    private readonly IRoundMatchesQuery _roundMatchesQuery = Substitute.For<IRoundMatchesQuery>();
    private readonly IUserRoundPredictionsQuery _predictionsQuery = Substitute.For<IUserRoundPredictionsQuery>();
    private readonly IPredictionLeaguesQuery _leaguesQuery = Substitute.For<IPredictionLeaguesQuery>();
    private readonly GetPredictionPageDataQueryHandler _handler;

    public GetPredictionPageDataQueryHandlerTests()
    {
        _handler = new GetPredictionPageDataQueryHandler(
            _roundHeaderQuery, _roundMatchesQuery, _predictionsQuery, _leaguesQuery);

        GivenRound();
        GivenMatches();
        GivenPredictions();
        GivenLeagues(new PredictionLeaguesData([], [], []));
    }

    #region The round

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThereIsNoSuchRound()
    {
        // Arrange
        _roundHeaderQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((RoundHeaderRow?)null);

        // Act
        var page = await HandleAsync();

        // Assert
        page.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoundAndItsSeason()
    {
        // Arrange
        GivenRound(roundNumber: 12);

        // Act
        var page = await HandleAsync();

        // Assert
        page!.RoundId.Should().Be(RoundId);
        page.RoundNumber.Should().Be(12);
        page.RoundDisplayName.Should().Be("Gameweek 12");
        page.SeasonName.Should().Be("2026/27");
        page.DeadlineUtc.Should().Be(KickOff.AddHours(-2));
    }

    [Theory]
    [InlineData(CompetitionType.Tournament, true)]
    [InlineData(CompetitionType.League, false)]
    public async Task Handle_ShouldSayWhetherTheCompetitionIsATournament(CompetitionType competitionType, bool expected)
    {
        // The page names a tournament round by its stage and a league round by its number, so this decides which.
        GivenRound(competitionType: competitionType);

        // Act
        var page = await HandleAsync();

        // Assert
        page!.IsTournament.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldSayWhenThisIsTheLastRoundOfTheSeason()
    {
        // Arrange
        GivenRound(roundNumber: 38, numberOfRounds: 38);

        // Act
        var page = await HandleAsync();

        // Assert
        page!.IsLastRoundOfSeason.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotSayItIsTheLastRound_WhenThereAreMoreToCome()
    {
        // Arrange
        GivenRound(roundNumber: 37, numberOfRounds: 38);

        // Act
        var page = await HandleAsync();

        // Assert
        page!.IsLastRoundOfSeason.Should().BeFalse();
    }

    #endregion

    #region The fixtures

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyFormForARoundWithNoFixturesYet()
    {
        // Act
        var page = await HandleAsync();

        // Assert
        page!.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListTheFixturesInKickOffOrder()
    {
        // Arrange
        GivenMatches(Match(3, KickOff.AddHours(2)), Match(1, KickOff), Match(2, KickOff.AddHours(1)));

        // Act
        var page = await HandleAsync();

        // Assert
        page!.Matches.Select(match => match.MatchId).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAFixtureThatWasCalledOff()
    {
        // A called-off fixture cannot be predicted, so it has no row on the form.
        GivenMatches(Match(1, KickOff), Match(2, KickOff.AddHours(1)) with { Status = MatchStatus.Postponed });

        // Act
        var page = await HandleAsync();

        // Assert
        page!.Matches.Select(match => match.MatchId).Should().Equal(1);
    }

    [Fact]
    public async Task Handle_ShouldFillInWhatThePlayerHasAlreadyEntered()
    {
        // Arrange
        GivenMatches(Match(1, KickOff), Match(2, KickOff.AddHours(1)));
        GivenPredictions(new UserRoundPredictionRow(2, 3, 1, PredictionOutcome.Pending));

        // Act
        var page = await HandleAsync();

        // Assert
        var unpredicted = page!.Matches.Single(match => match.MatchId == 1);
        var predicted = page.Matches.Single(match => match.MatchId == 2);

        unpredicted.PredictedHomeScore.Should().BeNull();
        unpredicted.PredictedAwayScore.Should().BeNull();
        predicted.PredictedHomeScore.Should().Be(3);
        predicted.PredictedAwayScore.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldKeepAFixtureWhoseTeamsAreNotKnownYet()
    {
        // A tournament tie is on the form as soon as it is scheduled, marked as not yet predictable rather than hidden.
        GivenMatches(Match(1, KickOff) with
        {
            HomeTeamId = null,
            AwayTeamId = null,
            HomeTeamName = null,
            AwayTeamName = null,
            PlaceholderHomeName = "Winner of QF1",
            PlaceholderAwayName = "Winner of QF2"
        });

        // Act
        var match = (await HandleAsync())!.Matches.Single();

        // Assert
        match.AreTeamsConfirmed.Should().BeFalse();
        match.PlaceholderHomeName.Should().Be("Winner of QF1");
    }

    [Fact]
    public async Task Handle_ShouldTreatAFixtureWithOnlyOneTeamKnownAsUnconfirmed()
    {
        // A tie where one side has come through and the other has not. Both are needed before it can be predicted.
        GivenMatches(Match(1, KickOff) with { AwayTeamId = null, AwayTeamName = null, PlaceholderAwayName = "Winner of QF2" });

        // Act
        var match = (await HandleAsync())!.Matches.Single();

        // Assert
        match.AreTeamsConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportEachFixturesTeamsAndLockTime()
    {
        // Arrange
        GivenMatches(Match(1, KickOff) with { CustomLockTimeUtc = KickOff.AddHours(-1), MatchNumber = 4 });

        // Act
        var match = (await HandleAsync())!.Matches.Single();

        // Assert
        match.MatchDateTimeUtc.Should().Be(KickOff);
        match.MatchNumber.Should().Be(4);
        match.HomeTeamName.Should().Be("Arsenal");
        match.HomeTeamShortName.Should().Be("Arsenal");
        match.HomeTeamAbbreviation.Should().Be("ARS");
        match.HomeTeamLogoUrl.Should().Be("ars.png");
        match.AwayTeamName.Should().Be("Chelsea");
        match.AreTeamsConfirmed.Should().BeTrue();
        match.CustomLockTimeUtc.Should().Be(KickOff.AddHours(-1));
    }

    #endregion

    #region The leagues and their boosts

    [Fact]
    public async Task Handle_ShouldReturnNoLeagues_WhenThePlayerIsInNoneForThisSeason()
    {
        // Act
        var page = await HandleAsync();

        // Assert
        page!.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListTheirLeaguesByName()
    {
        // Arrange - the ids run the other way to the names, so ordering by whichever came to hand would fail.
        GivenLeagues(new PredictionLeaguesData(
            [new PredictionLeagueRow(100, "Zulu League"), new PredictionLeagueRow(300, "Alpha League")],
            [], []));

        // Act
        var page = await HandleAsync();

        // Assert
        page!.Leagues.Select(league => league.Name).Should().Equal("Alpha League", "Zulu League");
    }

    [Fact]
    public async Task Handle_ShouldSayALeagueRunsBoosts_WhenOneIsSwitchedOn()
    {
        // Arrange
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId)], []));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.HasBoosts.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayALeagueRunsNoBoosts_WhenEveryRuleIsSwitchedOff()
    {
        // Having rules recorded and all of them off is not the same as running boosts, which is why disabled rules have to
        // arrive rather than being filtered out by the read.
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId) with { IsEnabled = false }], []));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.HasBoosts.Should().BeFalse();
        league.HasUnusedBoostThisSeason.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSayABoostIsStillAvailable_WhenNoneHasBeenUsed()
    {
        // Arrange
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId)], []));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.HasUnusedBoostThisSeason.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayNoBoostIsAvailable_OnceTheOnlyOneHasBeenUsed()
    {
        // Arrange
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId)], [Usage(DoublePointsId, RoundId)]));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.HasUnusedBoostThisSeason.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldJudgeEachBoostSeparately()
    {
        // Having spent the double-points boost says nothing about whether the banker is still there.
        GivenLeagues(new PredictionLeaguesData(
            [League()],
            [Rule(DoublePointsId), Rule(BankerId)],
            [Usage(DoublePointsId, RoundId)]));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.HasUnusedBoostThisSeason.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayNoBoostIsAvailable_WhenTheRuleAllowsNoUses()
    {
        // A rule switched on with an allowance of nothing offers nothing.
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId) with { TotalUsesPerSeason = 0 }], []));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.HasBoosts.Should().BeTrue();
        league.HasUnusedBoostThisSeason.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotCountAnotherLeaguesBoostRuleOrUsage()
    {
        // Arrange
        GivenLeagues(new PredictionLeaguesData(
            [League(), new PredictionLeagueRow(200, "Bravo League")],
            [Rule(DoublePointsId), Rule(DoublePointsId, leagueId: 200)],
            [Usage(DoublePointsId, RoundId, leagueId: 200)]));

        // Act
        var page = await HandleAsync();

        // Assert
        page!.Leagues.Single(league => league.LeagueId == LeagueId).HasUnusedBoostThisSeason.Should().BeTrue();
        page.Leagues.Single(league => league.LeagueId == 200).HasUnusedBoostThisSeason.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportTheBoostAlreadyPickedForThisRound()
    {
        // Arrange
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId)], [Usage(DoublePointsId, RoundId)]));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.SelectedBoostCode.Should().Be("DOUBLE");
    }

    [Fact]
    public async Task Handle_ShouldNotReportABoostUsedInAnotherRoundAsThisRoundsPick()
    {
        // The same rows answer two questions - what is left for the season, and what is picked for this round - and only one
        // of them cares which round it was.
        GivenLeagues(new PredictionLeaguesData([League()], [Rule(DoublePointsId)], [Usage(DoublePointsId, RoundId + 1)]));

        // Act
        var league = (await HandleAsync())!.Leagues.Single();

        // Assert
        league.SelectedBoostCode.Should().BeNull();
        league.HasUnusedBoostThisSeason.Should().BeFalse();
    }

    #endregion

    [Fact]
    public async Task Handle_ShouldAskForTheRoundAndSeasonRequested()
    {
        // Act
        await HandleAsync();

        // Assert - the season comes from the round, so the leagues cannot be for a different one.
        await _roundHeaderQuery.Received(1).ExecuteAsync(RoundId, Arg.Any<CancellationToken>());
        await _roundMatchesQuery.Received(1).ExecuteAsync(RoundId, Arg.Any<CancellationToken>());
        await _predictionsQuery.Received(1).ExecuteAsync(UserId, RoundId, Arg.Any<CancellationToken>());
        await _leaguesQuery.Received(1).ExecuteAsync(UserId, SeasonId, Arg.Any<CancellationToken>());
    }

    private void GivenRound(
        int roundNumber = 12,
        int numberOfRounds = 38,
        CompetitionType competitionType = CompetitionType.League) =>
        _roundHeaderQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new RoundHeaderRow(RoundId, roundNumber, $"Gameweek {roundNumber}", KickOff.AddHours(-2),
                SeasonId, "2026/27", numberOfRounds, competitionType));

    private void GivenMatches(params RoundMatchRow[] matches) =>
        _roundMatchesQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(matches);

    private void GivenPredictions(params UserRoundPredictionRow[] predictions) =>
        _predictionsQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(predictions);

    private void GivenLeagues(PredictionLeaguesData data) =>
        _leaguesQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(data);

    private static RoundMatchRow Match(int id, DateTime kickOffUtc) =>
        new(id, kickOffUtc, MatchNumber: id,
            HomeTeamId: 10, HomeTeamName: "Arsenal", HomeTeamShortName: "Arsenal", HomeTeamAbbreviation: "ARS",
            HomeTeamLogoUrl: "ars.png",
            AwayTeamId: 20, AwayTeamName: "Chelsea", AwayTeamShortName: "Chelsea", AwayTeamAbbreviation: "CHE",
            AwayTeamLogoUrl: "che.png",
            ActualHomeTeamScore: null, ActualAwayTeamScore: null, MatchStatus.Scheduled,
            PlaceholderHomeName: null, PlaceholderAwayName: null, CustomLockTimeUtc: null);

    private static PredictionLeagueRow League() => new(LeagueId, "Alpha League");

    private static PredictionBoostRuleRow Rule(int boostDefinitionId, int leagueId = LeagueId) =>
        new(leagueId, boostDefinitionId, IsEnabled: true, TotalUsesPerSeason: 2);

    private static PredictionBoostUsageRow Usage(int boostDefinitionId, int roundId, int leagueId = LeagueId) =>
        new(leagueId, boostDefinitionId, roundId, boostDefinitionId == DoublePointsId ? "DOUBLE" : "BANKER");

    private Task<PredictionPageDto?> HandleAsync() =>
        _handler.Handle(new GetPredictionPageDataQuery(RoundId, UserId), CancellationToken.None);
}
