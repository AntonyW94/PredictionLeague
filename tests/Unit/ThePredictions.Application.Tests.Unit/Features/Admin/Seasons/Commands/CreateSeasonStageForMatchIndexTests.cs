using FluentAssertions;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// When a tournament round combines several knockout stages, the placeholder matches created for it
/// have to be handed out to the right stage. The sizes are fixed by the format (last 32 = 16 ties,
/// last 16 = 8, quarters = 4, semis = 2, third place = 1, final = 1).
/// </summary>
public class CreateSeasonStageForMatchIndexTests
{
    private static TournamentStage Stage(List<TournamentStage> stages, int matchIndex, int totalMatches) =>
        CreateSeasonCommandHandler.GetStageForMatchIndex(stages, matchIndex, totalMatches);

    [Theory]
    [InlineData(0, TournamentStage.SemiFinals)]
    [InlineData(1, TournamentStage.SemiFinals)]
    [InlineData(2, TournamentStage.ThirdPlace)]
    [InlineData(3, TournamentStage.Final)]
    public void GetStageForMatchIndex_ShouldSplitAFinalsWeekendAcrossItsThreeStages(int matchIndex, TournamentStage expected)
    {
        List<TournamentStage> stages = [TournamentStage.SemiFinals, TournamentStage.ThirdPlace, TournamentStage.Final];

        Stage(stages, matchIndex, totalMatches: 4).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, TournamentStage.QuarterFinals)]
    [InlineData(3, TournamentStage.QuarterFinals)]
    [InlineData(4, TournamentStage.SemiFinals)]
    [InlineData(5, TournamentStage.SemiFinals)]
    [InlineData(6, TournamentStage.Final)]
    public void GetStageForMatchIndex_ShouldGiveQuartersFourTiesAndSemisTwo(int matchIndex, TournamentStage expected)
    {
        List<TournamentStage> stages = [TournamentStage.QuarterFinals, TournamentStage.SemiFinals, TournamentStage.Final];

        Stage(stages, matchIndex, totalMatches: 7).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, TournamentStage.RoundOf32)]
    [InlineData(15, TournamentStage.RoundOf32)]
    [InlineData(16, TournamentStage.RoundOf16)]
    [InlineData(23, TournamentStage.RoundOf16)]
    public void GetStageForMatchIndex_ShouldGiveTheLast32SixteenTiesAndTheLast16Eight(int matchIndex, TournamentStage expected)
    {
        List<TournamentStage> stages = [TournamentStage.RoundOf32, TournamentStage.RoundOf16];

        Stage(stages, matchIndex, totalMatches: 24).Should().Be(expected);
    }

    [Fact]
    public void GetStageForMatchIndex_ShouldReturnTheOnlyStage_WhenTheRoundIsNotCombined()
    {
        List<TournamentStage> stages = [TournamentStage.Final];

        Stage(stages, 0, totalMatches: 1).Should().Be(TournamentStage.Final);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(11)]
    public void GetStageForMatchIndex_ShouldShareGroupMatchesEvenly_ForStagesWithNoFixedSize(int matchIndex)
    {
        // Group stages have no inherent tie count, so the total is divided by the stage count.
        List<TournamentStage> stages = [TournamentStage.Group1, TournamentStage.Group2, TournamentStage.Group3];

        var expected = matchIndex switch
        {
            < 4 => TournamentStage.Group1,
            < 8 => TournamentStage.Group2,
            _ => TournamentStage.Group3
        };

        Stage(stages, matchIndex, totalMatches: 12).Should().Be(expected);
    }

    [Fact]
    public void GetStageForMatchIndex_ShouldFallBackToTheLastStage_WhenThereAreMoreMatchesThanTheStagesAccountFor()
    {
        // The API occasionally reports an extra tie; it belongs with the latest stage rather than
        // failing the sync.
        List<TournamentStage> stages = [TournamentStage.SemiFinals, TournamentStage.Final];

        Stage(stages, matchIndex: 99, totalMatches: 100).Should().Be(TournamentStage.Final);
    }

    [Fact]
    public void GetStageForMatchIndex_ShouldFallBackToTheLastStage_WhenTheDivisionRoundsDownToNothing()
    {
        // totalMatches / stages.Count is integer division, so fewer matches than stages gives a
        // stage size of zero and every index falls through to the final stage.
        List<TournamentStage> stages = [TournamentStage.Group1, TournamentStage.Group2, TournamentStage.Group3];

        Stage(stages, matchIndex: 0, totalMatches: 2).Should().Be(TournamentStage.Group3);
    }

    [Fact]
    public void GetStageForMatchIndex_ShouldAssignEveryMatchInAFullKnockoutBracket()
    {
        List<TournamentStage> stages =
        [
            TournamentStage.RoundOf16, TournamentStage.QuarterFinals,
            TournamentStage.SemiFinals, TournamentStage.ThirdPlace, TournamentStage.Final
        ];

        var assigned = Enumerable.Range(0, 16).Select(i => Stage(stages, i, 16)).ToList();

        assigned.Count(s => s == TournamentStage.RoundOf16).Should().Be(8);
        assigned.Count(s => s == TournamentStage.QuarterFinals).Should().Be(4);
        assigned.Count(s => s == TournamentStage.SemiFinals).Should().Be(2);
        assigned.Count(s => s == TournamentStage.ThirdPlace).Should().Be(1);
        assigned.Count(s => s == TournamentStage.Final).Should().Be(1);
    }
}
