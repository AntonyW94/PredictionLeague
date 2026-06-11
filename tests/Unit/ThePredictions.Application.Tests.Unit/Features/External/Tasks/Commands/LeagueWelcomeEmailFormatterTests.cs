using FluentAssertions;
using ThePredictions.Application.Features.External.Tasks.Commands;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.External.Tasks.Commands;

public class LeagueWelcomeEmailFormatterTests
{
    private static LeagueWelcomeLeague CreateLeague(
        List<LeagueWelcomePrize>? prizes = null,
        List<LeagueWelcomeBoost>? boosts = null,
        int numberOfRounds = 7,
        int numberOfMonths = 2) =>
        new(
            LeagueId: 1,
            LeagueName: "Test League",
            SeasonName: "World Cup 2026",
            HasPrizes: true,
            MemberCount: 22,
            NumberOfRounds: numberOfRounds,
            NumberOfMonths: numberOfMonths,
            Prizes: prizes ?? [],
            Boosts: boosts ?? [],
            Recipients: []);

    [Fact]
    public void PrizePot_ShouldMultiplyRecurringPrizesByOccurrences()
    {
        // 135 overall + 6 x 7 rounds + 78 exact = 255
        var league = CreateLeague(prizes:
        [
            new LeagueWelcomePrize(PrizeType.Overall, 1, null, 135m),
            new LeagueWelcomePrize(PrizeType.Round, 1, null, 6m),
            new LeagueWelcomePrize(PrizeType.MostExactScores, 1, null, 78m)
        ]);

        LeagueWelcomeEmailFormatter.PrizePot(league).Should().Be("£255");
    }

    [Fact]
    public void PrizeLines_ShouldOrderOverallThenStagesThenRecurringThenExactScores()
    {
        var league = CreateLeague(prizes:
        [
            new LeagueWelcomePrize(PrizeType.MostExactScores, 1, null, 78m),
            new LeagueWelcomePrize(PrizeType.Round, 1, null, 6m),
            new LeagueWelcomePrize(PrizeType.Stages, 1, "Knockout stage", 45m),
            new LeagueWelcomePrize(PrizeType.Stages, 1, "Group stage", 45m),
            new LeagueWelcomePrize(PrizeType.Overall, 2, null, 65m),
            new LeagueWelcomePrize(PrizeType.Overall, 1, null, 135m)
        ]);

        var lines = LeagueWelcomeEmailFormatter.PrizeLines(league);

        lines.Select(l => l.Title).Should().ContainInOrder(
            "Overall - 1st",
            "Overall - 2nd",
            "Group stage - 1st",
            "Knockout stage - 1st",
            "Round winner - each of the 7 rounds",
            "Most exact scores");
        lines[0].Value.Should().Be("£135");
    }

    [Fact]
    public void PrizeLines_ShouldDescribeMonthlyPrizes_WhenPresent()
    {
        var league = CreateLeague(prizes: [new LeagueWelcomePrize(PrizeType.Monthly, 1, null, 10m)], numberOfMonths: 10);

        var lines = LeagueWelcomeEmailFormatter.PrizeLines(league);

        lines.Should().ContainSingle().Which.Title.Should().Be("Monthly winner - each of the 10 months");
    }

    [Fact]
    public void BoostLines_ShouldDescribeSeasonCapOnly_WhenSingleWindowSpansSeason()
    {
        var league = CreateLeague(boosts:
        [
            new LeagueWelcomeBoost("Double Up", "Doubles your round score.", 3,
                [new LeagueWelcomeBoostWindow(1, 7, 3)])
        ]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines.Should().ContainSingle();
        lines[0].Name.Should().Be("Double Up");
        lines[0].Description.Should().Be("Doubles your round score.");
        lines[0].Usage.Should().Be("Can be used 3 times this season");
    }

    [Fact]
    public void BoostLines_ShouldDescribeWindows_WhenWindowsRestrictRounds()
    {
        var league = CreateLeague(boosts:
        [
            new LeagueWelcomeBoost("Double Up", null, 3,
            [
                new LeagueWelcomeBoostWindow(1, 5, 2),
                new LeagueWelcomeBoostWindow(6, 7, 1)
            ])
        ]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines[0].Usage.Should().Be("Can be used 3 times this season: rounds 1-5 (max 2), rounds 6-7 (max 1)");
    }

    [Fact]
    public void BoostLines_ShouldUseSingularPhrasing_WhenOneUsePerSeason()
    {
        var league = CreateLeague(boosts: [new LeagueWelcomeBoost("Wildcard", null, 1, [])]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines[0].Usage.Should().Be("Can be used once this season");
    }

    [Fact]
    public void BoostLines_ShouldDescribeSingleRoundWindow_WhenWindowIsOneRound()
    {
        var league = CreateLeague(boosts:
        [
            new LeagueWelcomeBoost("Wildcard", null, 1, [new LeagueWelcomeBoostWindow(7, 7, 1)])
        ]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines[0].Usage.Should().Be("Can be used once this season: round 7 (max 1)");
    }
}
