using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// What a tournament stage is called on screen - one spelling, where the SQL had three.
/// </summary>
public class TournamentStageNameTests
{
    [Theory]
    [InlineData(TournamentStageGroup.GroupStage, "Group Stage")]
    [InlineData(TournamentStageGroup.KnockoutStage, "Knockout Stage")]
    public void For_ShouldNameTheStage(TournamentStageGroup stage, string expected)
    {
        TournamentStageName.For(stage).Should().Be(expected);
    }
}
