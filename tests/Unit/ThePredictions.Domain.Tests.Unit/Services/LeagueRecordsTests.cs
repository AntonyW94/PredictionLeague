using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Picking the holder of a league record. Ten SQL blocks did this with four different orderings between them, and
/// four of them stopped before any tie-break at all.
/// </summary>
public class LeagueRecordsTests
{
    private sealed record Candidate(string Name, int Score, int Round);

    private static Candidate? Highest(params Candidate[] candidates) =>
        LeagueRecords.Highest(candidates, c => c.Score, c => c.Round, c => c.Name);

    private static Candidate? Lowest(params Candidate[] candidates) =>
        LeagueRecords.Lowest(candidates, c => c.Score, c => c.Round, c => c.Name);

    [Fact]
    public void Highest_ShouldReturnTheBestScore()
    {
        Highest(new Candidate("Ada", 10, 1), new Candidate("Grace", 30, 2), new Candidate("Alan", 20, 3))!
            .Name.Should().Be("Grace");
    }

    [Fact]
    public void Highest_ShouldPreferTheEarlierRound_WhenScoresTie()
    {
        // The record belongs to whoever got there first.
        Highest(new Candidate("Ada", 30, 5), new Candidate("Grace", 30, 2))!.Name.Should().Be("Grace");
    }

    [Fact]
    public void Highest_ShouldFallBackToTheName_WhenScoreAndRoundBothTie()
    {
        // Four of the old blocks had no tie-break here at all, so the named holder was the query plan's choice.
        Highest(new Candidate("Grace", 30, 2), new Candidate("Ada", 30, 2))!.Name.Should().Be("Ada");
    }

    [Fact]
    public void Highest_ShouldIgnoreCase_WhenFallingBackToTheName()
    {
        Highest(new Candidate("bob", 30, 2), new Candidate("Alice", 30, 2))!.Name.Should().Be("Alice");
    }

    [Fact]
    public void Highest_ShouldReturnNothing_WhenThereAreNoCandidates()
    {
        Highest().Should().BeNull();
    }

    [Fact]
    public void Lowest_ShouldReturnTheWorstScore()
    {
        Lowest(new Candidate("Ada", 10, 1), new Candidate("Grace", 30, 2))!.Name.Should().Be("Ada");
    }

    [Fact]
    public void Lowest_ShouldStillPreferTheEarlierRound_WhenScoresTie()
    {
        // The tie-break runs forwards even for the record nobody wants: of two equally bad rounds, the earlier.
        Lowest(new Candidate("Ada", 2, 5), new Candidate("Grace", 2, 2))!.Name.Should().Be("Grace");
    }

    [Fact]
    public void Lowest_ShouldFallBackToTheName_WhenScoreAndRoundBothTie()
    {
        Lowest(new Candidate("Grace", 2, 2), new Candidate("Ada", 2, 2))!.Name.Should().Be("Ada");
    }

    [Fact]
    public void Lowest_ShouldReturnNothing_WhenThereAreNoCandidates()
    {
        Lowest().Should().BeNull();
    }
}
