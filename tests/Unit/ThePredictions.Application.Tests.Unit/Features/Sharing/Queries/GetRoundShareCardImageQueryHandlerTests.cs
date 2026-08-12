using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Features.Sharing.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Sharing.Queries;

/// <summary>
/// Builds the shareable image of someone's predictions for a round. It only ever shows fixtures they actually predicted, and
/// only reveals a real score once the match is genuinely under way - so the card cannot leak a result the recipient could not
/// otherwise see.
/// </summary>
/// <remarks>
/// The fixtures and the predictions now arrive through the same two ports the prediction page uses, which is why the card and
/// the form cannot disagree about which fixtures exist or what was entered.
/// </remarks>
public class GetRoundShareCardImageQueryHandlerTests
{
    private const int RoundId = 100;
    private const string UserId = "user-1";

    private static readonly DateTime KickOff = new(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly IRoundHeaderQuery _roundHeaderQuery = Substitute.For<IRoundHeaderQuery>();
    private readonly IRoundMatchesQuery _roundMatchesQuery = Substitute.For<IRoundMatchesQuery>();
    private readonly IUserRoundPredictionsQuery _predictionsQuery = Substitute.For<IUserRoundPredictionsQuery>();
    private readonly IShareCardPlayerQuery _playerQuery = Substitute.For<IShareCardPlayerQuery>();
    private readonly IShareCardRenderer _renderer = Substitute.For<IShareCardRenderer>();

    private readonly GetRoundShareCardImageQueryHandler _handler;

    public GetRoundShareCardImageQueryHandlerTests()
    {
        _handler = new GetRoundShareCardImageQueryHandler(
            _roundHeaderQuery, _roundMatchesQuery, _predictionsQuery, _playerQuery, _renderer);

        _renderer.RenderAsync(Arg.Any<ShareCardModel>(), Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
    }

    /// <summary>One fixture and what the player put against it, before the two are split across their ports.</summary>
    private sealed record CardFixture(RoundMatchRow Match, UserRoundPredictionRow? Prediction);

    private void GivenRound(
        int roundNumber = 5,
        string? roundDisplayName = "Gameweek 5",
        CompetitionType competitionType = CompetitionType.League,
        string? playerFirstName = "Alice",
        string? preferredTheme = "light")
    {
        _roundHeaderQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new RoundHeaderRow(RoundId, roundNumber, roundDisplayName ?? string.Empty, KickOff.AddHours(-2),
                SeasonId: 7, "2026/27", NumberOfRounds: 38, competitionType));

        _playerQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ShareCardPlayerRow(playerFirstName, preferredTheme));
    }

    /// <summary>
    /// Splits the fixtures across the two ports, giving each a kick-off an hour after the last so the order they are written
    /// in is the order the card is meant to read in.
    /// </summary>
    private void GivenMatches(params CardFixture[] fixtures)
    {
        var matches = fixtures
            .Select((fixture, index) => fixture.Match with { Id = index + 1, MatchDateTimeUtc = KickOff.AddHours(index) })
            .ToList();

        var predictions = fixtures
            .Select((fixture, index) => fixture.Prediction is null ? null : fixture.Prediction with { MatchId = index + 1 })
            .OfType<UserRoundPredictionRow>()
            .ToList();

        _roundMatchesQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(matches);
        _predictionsQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(predictions);
    }

    private static CardFixture MatchRow(
        int? predictedHome = 2,
        int? predictedAway = 1,
        MatchStatus status = MatchStatus.Completed,
        int? actualHome = 2,
        int? actualAway = 1,
        PredictionOutcome outcome = PredictionOutcome.ExactScore,
        string homeShortName = "Arsenal") =>
        new(
            new RoundMatchRow(
                Id: 0, KickOff, MatchNumber: null,
                HomeTeamId: 10, HomeTeamName: homeShortName, HomeTeamShortName: homeShortName, HomeTeamAbbreviation: "ARS",
                HomeTeamLogoUrl: "ars.png",
                AwayTeamId: 20, AwayTeamName: "Chelsea", AwayTeamShortName: "Chelsea", AwayTeamAbbreviation: "CHE",
                AwayTeamLogoUrl: "che.png",
                actualHome, actualAway, status,
                PlaceholderHomeName: null, PlaceholderAwayName: null, CustomLockTimeUtc: null),
            new UserRoundPredictionRow(MatchId: 0, predictedHome, predictedAway, outcome));

    /// <summary>A fixture whose teams are not known yet, which has nothing to draw.</summary>
    private static CardFixture PlaceholderRow() =>
        MatchRow() with
        {
            Match = MatchRow().Match with
            {
                HomeTeamId = null, HomeTeamName = null, HomeTeamShortName = null, HomeTeamAbbreviation = null,
                AwayTeamId = null, AwayTeamName = null, AwayTeamShortName = null, AwayTeamAbbreviation = null,
                PlaceholderHomeName = "Winner of QF1", PlaceholderAwayName = "Winner of QF2"
            }
        };

    private ShareCardModel CapturedModel() =>
        (ShareCardModel)_renderer.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IShareCardRenderer.RenderAsync))
            .GetArguments()[0]!;

    private Task<byte[]?> HandleAsync(string? theme = null) =>
        _handler.Handle(new GetRoundShareCardImageQuery(RoundId, UserId, theme), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheRoundDoesNotExist()
    {
        var result = await HandleAsync();

        result.Should().BeNull();
        await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheyPredictedNothingInThisRound()
    {
        // There is no card to draw, and returning an empty one would look like a bug.
        GivenRound();

        var result = await HandleAsync();

        result.Should().BeNull();
        await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenEveryFixtureWasLeftBlank()
    {
        GivenRound();
        GivenMatches(MatchRow() with { Prediction = null });

        var result = await HandleAsync();

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, null)]
    public async Task Handle_ShouldLeaveOutAHalfEnteredPrediction(int? predictedHome, int? predictedAway)
    {
        // A prediction needs both scores to mean anything, so a half-filled row is dropped rather
        // than rendered as a zero.
        GivenRound();
        GivenMatches(
            MatchRow(predictedHome, predictedAway, homeShortName: "Arsenal"),
            MatchRow(homeShortName: "Spurs"));

        await HandleAsync();

        CapturedModel().Matches.Should().ContainSingle().Which.HomeTeamShortName.Should().Be("Spurs");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAFixtureWhoseTeamsAreNotKnownYet()
    {
        // There is nothing to draw for "Winner of QF1" against "Winner of QF2", and the fixture cannot have been predicted
        // in any meaningful way. The statement this replaces excluded these by joining the teams rather than saying so.
        GivenRound();
        GivenMatches(PlaceholderRow(), MatchRow(homeShortName: "Spurs"));

        await HandleAsync();

        CapturedModel().Matches.Should().ContainSingle().Which.HomeTeamShortName.Should().Be("Spurs");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAFixtureThatWasCalledOff()
    {
        // A postponed fixture is not part of the round the player predicted.
        GivenRound();
        GivenMatches(
            MatchRow(status: MatchStatus.Postponed, homeShortName: "Arsenal"),
            MatchRow(homeShortName: "Spurs"));

        await HandleAsync();

        CapturedModel().Matches.Should().ContainSingle().Which.HomeTeamShortName.Should().Be("Spurs");
    }

    [Fact]
    public async Task Handle_ShouldStillDrawACard_WhenThereIsNoSuchPlayer()
    {
        // The round and the predictions exist, so there is a card to draw; it just has no name on it and falls back to the
        // application's default theme rather than failing.
        GivenRound();
        GivenMatches(MatchRow());
        _playerQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ShareCardPlayerRow?)null);

        // Act
        var result = await HandleAsync();

        // Assert
        result.Should().Equal(1, 2, 3);
        CapturedModel().PlayerName.Should().BeNull();
        CapturedModel().Theme.Should().Be(ShareCardTheme.Light);
    }

    [Fact]
    public async Task Handle_ShouldRenderTheImage()
    {
        GivenRound();
        GivenMatches(MatchRow());

        var result = await HandleAsync();

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldCarryThePredictionOntoTheCard()
    {
        GivenRound();
        GivenMatches(MatchRow(predictedHome: 3, predictedAway: 0, outcome: PredictionOutcome.CorrectResult));

        await HandleAsync();

        var match = CapturedModel().Matches.Single();
        match.HomeTeamShortName.Should().Be("Arsenal");
        match.HomeTeamAbbreviation.Should().Be("ARS");
        match.HomeTeamLogoUrl.Should().Be("ars.png");
        match.AwayTeamShortName.Should().Be("Chelsea");
        match.PredictedHomeScore.Should().Be(3);
        match.PredictedAwayScore.Should().Be(0);
        match.Outcome.Should().Be(PredictionOutcome.CorrectResult);
    }

    [Theory]
    [InlineData(MatchStatus.InProgress, true)]
    [InlineData(MatchStatus.Completed, true)]
    [InlineData(MatchStatus.Scheduled, false)]
    public async Task Handle_ShouldOnlyShowTheRealScoreOnceTheMatchIsUnderWay(MatchStatus status, bool expectedScored)
    {
        // A scheduled match must not display a score even if one has somehow been recorded - that
        // would leak a result before kick-off.
        GivenRound();
        GivenMatches(MatchRow(status: status, actualHome: 2, actualAway: 1));

        await HandleAsync();

        CapturedModel().Matches.Single().IsScored.Should().Be(expectedScored);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public async Task Handle_ShouldNotShowAScoreThatIsOnlyHalfRecorded(int? actualHome, int? actualAway)
    {
        GivenRound();
        GivenMatches(MatchRow(status: MatchStatus.Completed, actualHome: actualHome, actualAway: actualAway));

        await HandleAsync();

        CapturedModel().Matches.Single().IsScored.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldLabelALeagueRoundByItsNumber()
    {
        // League gameweeks are numbered, and a stored display name would just repeat that.
        GivenRound(roundNumber: 5, roundDisplayName: "Gameweek 5", competitionType: CompetitionType.League);
        GivenMatches(MatchRow());

        await HandleAsync();

        CapturedModel().RoundLabel.Should().Be("Round 5");
    }

    [Fact]
    public async Task Handle_ShouldLabelAKnockoutRoundByItsName()
    {
        // "Semi-finals" means far more on a shared card than "Round 3".
        GivenRound(roundNumber: 3, roundDisplayName: "Semi-finals", competitionType: CompetitionType.Tournament);
        GivenMatches(MatchRow());

        await HandleAsync();

        CapturedModel().RoundLabel.Should().Be("Semi-finals");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldFallBackToTheRoundNumber_WhenAKnockoutRoundHasNoName(string? displayName)
    {
        GivenRound(roundNumber: 3, roundDisplayName: displayName, competitionType: CompetitionType.Tournament);
        GivenMatches(MatchRow());

        await HandleAsync();

        CapturedModel().RoundLabel.Should().Be("Round 3");
    }

    [Fact]
    public async Task Handle_ShouldNameThePlayerOnTheCard()
    {
        GivenRound(playerFirstName: "Alice");
        GivenMatches(MatchRow());

        await HandleAsync();

        var model = CapturedModel();
        model.PlayerName.Should().Be("Alice");
        model.SeasonName.Should().Be("2026/27");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldLeaveThePlayerNameOff_WhenThereIsNothingToShow(string? firstName)
    {
        // Better an unnamed card than one greeting a blank space.
        GivenRound(playerFirstName: firstName);
        GivenMatches(MatchRow());

        await HandleAsync();

        CapturedModel().PlayerName.Should().BeNull();
    }

    [Theory]
    [InlineData("dark", ShareCardTheme.Dark)]
    [InlineData("DARK", ShareCardTheme.Dark)]
    [InlineData("light", ShareCardTheme.Light)]
    [InlineData("something-else", ShareCardTheme.Light)]
    public async Task Handle_ShouldTakeTheThemeTheScreenIsCurrentlyShowing(string requestedTheme, ShareCardTheme expected)
    {
        // What the user is looking at wins over their saved preference, so the card matches the
        // screen it was shared from.
        GivenRound(preferredTheme: "light");
        GivenMatches(MatchRow());

        await HandleAsync(theme: requestedTheme);

        CapturedModel().Theme.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldFallBackToTheSavedTheme_WhenTheScreenDidNotSayWhichItWasShowing(string? requestedTheme)
    {
        GivenRound(preferredTheme: "dark");
        GivenMatches(MatchRow());

        await HandleAsync(theme: requestedTheme);

        CapturedModel().Theme.Should().Be(ShareCardTheme.Dark);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheLightCard_WhenNoThemeIsKnownAtAll()
    {
        // Light is the app's own default, so an account that never chose one gets the same look.
        GivenRound(preferredTheme: null);
        GivenMatches(MatchRow());

        await HandleAsync();

        CapturedModel().Theme.Should().Be(ShareCardTheme.Light);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheFixturesInTheOrderTheyWereRead()
    {
        // Kick-off order, which the handler applies through the same rule the prediction page uses.
        GivenRound();
        GivenMatches(
            MatchRow(homeShortName: "Arsenal"),
            MatchRow(homeShortName: "Spurs"),
            MatchRow(homeShortName: "Chelsea"));

        await HandleAsync();

        CapturedModel().Matches.Select(m => m.HomeTeamShortName)
            .Should().Equal("Arsenal", "Spurs", "Chelsea");
    }
}
