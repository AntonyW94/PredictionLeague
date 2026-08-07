using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Features.Sharing.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Features.Sharing.Queries.GetRoundShareCardImageQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Sharing.Queries;

/// <summary>
/// Builds the shareable image of someone's predictions for a round. It only ever shows fixtures
/// they actually predicted, and only reveals a real score once the match is genuinely under way -
/// so the card cannot leak a result the recipient could not otherwise see.
/// </summary>
public class GetRoundShareCardImageQueryHandlerTests
{
    private const int RoundId = 100;
    private const string UserId = "user-1";

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IShareCardRenderer _renderer = Substitute.For<IShareCardRenderer>();

    private readonly GetRoundShareCardImageQueryHandler _handler;

    public GetRoundShareCardImageQueryHandlerTests()
    {
        _handler = new GetRoundShareCardImageQueryHandler(_dbConnection, _renderer);
        _renderer.RenderAsync(Arg.Any<ShareCardModel>(), Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
    }

    private void GivenRound(
        int roundNumber = 5,
        string? roundDisplayName = "Gameweek 5",
        CompetitionType competitionType = CompetitionType.League,
        string? playerFirstName = "Alice",
        string? preferredTheme = "light") =>
        _dbConnection.QuerySingleOrDefaultAsync<ShareCardRoundResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new ShareCardRoundResult(roundNumber, roundDisplayName, "2026/27",
                (int)competitionType, playerFirstName, preferredTheme));

    private void GivenMatches(params ShareCardMatchResult[] matches) =>
        _dbConnection.QueryAsync<ShareCardMatchResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(matches);

    private static ShareCardMatchResult MatchRow(
        int? predictedHome = 2,
        int? predictedAway = 1,
        MatchStatus status = MatchStatus.Completed,
        int? actualHome = 2,
        int? actualAway = 1,
        PredictionOutcome outcome = PredictionOutcome.ExactScore,
        string homeShortName = "Arsenal") =>
        new(homeShortName, "ARS", "ars.png", "Chelsea", "CHE", "che.png",
            predictedHome, predictedAway, outcome, status.ToString(), actualHome, actualAway);

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
        GivenMatches(MatchRow(predictedHome: null, predictedAway: null));

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
        // The SQL orders by kick-off, and the card is meant to read in that order.
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
