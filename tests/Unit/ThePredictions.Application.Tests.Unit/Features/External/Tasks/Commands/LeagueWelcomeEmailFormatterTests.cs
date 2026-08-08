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
    public void PrizeSections_ShouldGroupIntoOverallThenStagesThenOther()
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

        var sections = LeagueWelcomeEmailFormatter.PrizeSections(league);

        sections.Select(s => s.Title).Should().ContainInOrder("Overall", "Group stage", "Knockout stage", "Other prizes");
    }

    [Fact]
    public void PrizeSections_ShouldFlagOnlyTheTopPrizeOfRankedSections()
    {
        var league = CreateLeague(prizes:
        [
            new LeagueWelcomePrize(PrizeType.Overall, 1, null, 135m),
            new LeagueWelcomePrize(PrizeType.Overall, 2, null, 65m),
            new LeagueWelcomePrize(PrizeType.Stages, 1, "Group stage", 45m),
            new LeagueWelcomePrize(PrizeType.MostExactScores, 1, null, 78m)
        ]);

        var sections = LeagueWelcomeEmailFormatter.PrizeSections(league);

        var overall = sections.Single(s => s.Title == "Overall");
        overall.Prizes[0].Should().Be(new LeagueWelcomePrizeLine("1st place", "£135", IsTop: true));
        overall.Prizes[1].Should().Be(new LeagueWelcomePrizeLine("2nd place", "£65", IsTop: false));

        sections.Single(s => s.Title == "Group stage").Prizes.Single().IsTop.Should().BeTrue();
        sections.Single(s => s.Title == "Other prizes").Prizes.Single().IsTop.Should().BeFalse();
    }

    [Fact]
    public void PrizeSections_ShouldDescribeRecurringPrizes_InOtherSection()
    {
        var league = CreateLeague(
            prizes:
            [
                new LeagueWelcomePrize(PrizeType.Round, 1, null, 6m),
                new LeagueWelcomePrize(PrizeType.Monthly, 1, null, 10m)
            ],
            numberOfMonths: 10);

        var sections = LeagueWelcomeEmailFormatter.PrizeSections(league);

        var other = sections.Single(s => s.Title == "Other prizes");
        other.Prizes.Select(p => p.Title).Should().ContainInOrder(
            "Round winner - each of the 7 rounds",
            "Monthly winner - each of the 10 months");
    }

    [Fact]
    public void PrizeSections_ShouldOmitEmptySections()
    {
        var league = CreateLeague(prizes: [new LeagueWelcomePrize(PrizeType.Overall, 1, null, 135m)]);

        var sections = LeagueWelcomeEmailFormatter.PrizeSections(league);

        sections.Should().ContainSingle().Which.Title.Should().Be("Overall");
    }

    [Fact]
    public void BoostLines_ShouldIncludeImageUrl()
    {
        var league = CreateLeague(boosts:
        [
            new LeagueWelcomeBoost("Double Up", "Doubles your round score.", "/images/boosts/double-up-normal.png", 3,
                [new LeagueWelcomeBoostWindow(1, 7, 3)])
        ]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines.Should().ContainSingle();
        lines[0].Name.Should().Be("Double Up");
        lines[0].Description.Should().Be("Doubles your round score.");
        lines[0].ImageUrl.Should().Be("/images/boosts/double-up-normal.png");
        lines[0].Usage.Should().Be("Can be used 3 times this season");
    }

    [Fact]
    public void BoostLines_ShouldDescribeWindows_WhenWindowsRestrictRounds()
    {
        var league = CreateLeague(boosts:
        [
            new LeagueWelcomeBoost("Double Up", null, null, 3,
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
        var league = CreateLeague(boosts: [new LeagueWelcomeBoost("Wildcard", null, null, 1, [])]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines[0].Usage.Should().Be("Can be used once this season");
    }

    [Fact]
    public void BoostLines_ShouldDescribeSingleRoundWindow_WhenWindowIsOneRound()
    {
        var league = CreateLeague(boosts:
        [
            new LeagueWelcomeBoost("Wildcard", null, null, 1, [new LeagueWelcomeBoostWindow(7, 7, 1)])
        ]);

        var lines = LeagueWelcomeEmailFormatter.BoostLines(league);

        lines[0].Usage.Should().Be("Can be used once this season: round 7 (max 1)");
    }

    [Fact]
    public void PrizePot_ShouldMultiplyAMonthlyPrizeByTheMonthsTheSeasonRuns()
    {
        // A monthly prize is won once a month, so the pot counts it once per month rather than once.
        var league = CreateLeague(prizes: [new LeagueWelcomePrize(PrizeType.Monthly, 1, null, 10m)]);

        LeagueWelcomeEmailFormatter.PrizePot(league).Should().Be("£20");
    }

    [Fact]
    public void PrizeSections_ShouldFallBackToAGenericHeading_WhenAStagePrizeHasNoStageName()
    {
        // Better a section headed "Stage" than one headed with a blank space.
        var league = CreateLeague(prizes: [new LeagueWelcomePrize(PrizeType.Stages, 1, "   ", 45m)]);

        var sections = LeagueWelcomeEmailFormatter.PrizeSections(league);

        sections.Should().ContainSingle();
        sections[0].Title.Should().Be("Stage");
    }
}
