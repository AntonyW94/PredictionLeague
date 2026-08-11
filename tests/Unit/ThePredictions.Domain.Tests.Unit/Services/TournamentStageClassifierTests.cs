using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// The group-or-knockout classification, previously a <c>LIKE '%Group%'</c> written out in nine places.
/// </summary>
public class TournamentStageClassifierTests
{
    [Theory]
    [InlineData("Group Stage")]
    [InlineData("Group")]
    [InlineData("Groups A-F")]
    [InlineData("Group Stage, Matchday 3")]
    public void ClassifyFrom_ShouldBeGroupStage_WhenTheTextMentionsAGroup(string stages)
    {
        TournamentStageClassifier.ClassifyFrom(stages).Should().Be(TournamentStageGroup.GroupStage);
    }

    [Theory]
    [InlineData("group stage")]
    [InlineData("GROUP STAGE")]
    public void ClassifyFrom_ShouldIgnoreCase_MatchingWhatTheDatabaseCollationDid(string stages)
    {
        // LIKE '%Group%' was case-insensitive because the database collation is, so the C# has to be too or
        // rounds would reclassify on the way across.
        TournamentStageClassifier.ClassifyFrom(stages).Should().Be(TournamentStageGroup.GroupStage);
    }

    [Theory]
    [InlineData("Round of 16")]
    [InlineData("Quarter-finals")]
    [InlineData("Final")]
    [InlineData("")]
    public void ClassifyFrom_ShouldBeKnockoutStage_WhenTheTextMentionsNoGroup(string stages)
    {
        TournamentStageClassifier.ClassifyFrom(stages).Should().Be(TournamentStageGroup.KnockoutStage);
    }

    [Fact]
    public void ClassifyFrom_ShouldBeKnockoutStage_WhenThereIsNoMappingAtAll()
    {
        // The old CASE had no null arm, so a null Stages fell to the ELSE. Preserved rather than improved:
        // changing it would reclassify unmapped rounds.
        TournamentStageClassifier.ClassifyFrom(null).Should().Be(TournamentStageGroup.KnockoutStage);
    }
}
