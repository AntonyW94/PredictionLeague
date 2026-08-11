using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// The tie policy behind every leaderboard in the application, previously fourteen <c>RANK() OVER</c> clauses
/// across nine query handlers.
///
/// The test that matters most is
/// <see cref="ByDescending_ShouldLeaveAGapAfterATie_SoNobodyTakesTheSkippedPosition"/>. Getting that wrong
/// does not throw, does not fail a build and does not look wrong in isolation - it silently shifts the
/// position shown to every player below a tie, on every leaderboard at once. Its message names the two
/// alternatives it rules out, rather than asserting them separately: a test that only says what the answer
/// is *not* passes for every other wrong answer too.
/// </summary>
public class RankingTests
{
    private sealed record Player(string FirstName, string LastName, int Points);

    [Fact]
    public void ByDescending_ShouldOrderHighestFirst()
    {
        var ranked = Rank(("Ada", 80), ("Grace", 100), ("Alan", 90));

        ranked.Select(r => r.Item.FirstName).Should().Equal("Grace", "Alan", "Ada");
        ranked.Select(r => r.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ByDescending_ShouldLeaveAGapAfterATie_SoNobodyTakesTheSkippedPosition()
    {
        // 100, 90, 90, 80 gives 1st, 2nd, 2nd, 4th. Nobody is 3rd: two players are ahead of the player on 80.
        // This is the rule. Losing it renumbers every player below every tie on every leaderboard.
        var ranked = Rank(("Grace", 100), ("Alan", 90), ("Ada", 90), ("Katherine", 80));

        ranked.Select(r => r.Rank).Should().Equal([1, 2, 2, 4],
            "this is the whole rule. DENSE_RANK() would give 1, 2, 2, 3 and tell the player on 80 they are "
            + "third when two players are above them; ROW_NUMBER() would give 1, 2, 3, 4 and hide the tie "
            + "altogether. Changing this renumbers every player below every tie, on every leaderboard.");
    }

    [Fact]
    public void ByDescending_ShouldShareFirstPlace_WhenSeveralLeadOnTheSameScore()
    {
        // Three joint leaders means the next player is 4th, not 2nd.
        var ranked = Rank(("Grace", 100), ("Alan", 100), ("Ada", 100), ("Katherine", 50));

        ranked.Select(r => r.Rank).Should().Equal(1, 1, 1, 4);
    }

    [Fact]
    public void ByDescending_ShouldGiveEveryoneFirstPlace_WhenAllScoresAreEqual()
    {
        Rank(("Grace", 40), ("Alan", 40), ("Ada", 40))
            .Select(r => r.Rank).Should().Equal(1, 1, 1);
    }

    [Fact]
    public void ByDescending_ShouldNeedNoGap_WhenTheTieIsAtTheBottom()
    {
        Rank(("Grace", 100), ("Alan", 90), ("Ada", 90))
            .Select(r => r.Rank).Should().Equal(1, 2, 2);
    }

    [Fact]
    public void ByDescending_ShouldHandleSeveralSeparateTies()
    {
        // These expectations were taken from SQL Server rather than reasoned out. Running
        //
        //   RANK() OVER (ORDER BY [Points] DESC)
        //
        // over exactly this data returns 1, 1, 3, 4, 4, 6 - so this is a direct check that the C# reproduces
        // what the fourteen RANK() clauses were doing. The same query returns 1, 1, 2, 3, 3, 4 for
        // DENSE_RANK() and 1, 2, 3, 4, 5, 6 for ROW_NUMBER(), neither of which is what we want.
        var ranked = Rank(("a", 100), ("b", 100), ("c", 90), ("d", 80), ("e", 80), ("f", 70));

        ranked.Select(r => r.Rank).Should().Equal(1, 1, 3, 4, 4, 6);
    }

    [Fact]
    public void ByDescending_ShouldRankASinglePlayerFirst()
    {
        Rank(("Ada", 0)).Select(r => r.Rank).Should().Equal(1);
    }

    [Fact]
    public void ByDescending_ShouldReturnEmpty_WhenThereAreNoPlayers()
    {
        Ranking.ByDescending(Array.Empty<Player>(), p => p.Points, p => p.FirstName).Should().BeEmpty();
    }

    [Fact]
    public void ByDescending_ShouldRankAZeroScoreLast_RatherThanExcludingIt()
    {
        // The SQL wrapped these keys in COALESCE(..., 0), so a member with no result recorded scores zero and
        // appears last rather than vanishing. That coercion is the caller's, expressed in the selector - this
        // pins that zero is a real score and gets a position.
        var players = new[] { new Player("Ada", "Lovelace", 0), new Player("Grace", "Hopper", 10) };

        var ranked = Ranking.ByDescending(players, p => p.Points, p => p.FirstName);

        ranked.Select(r => r.Item.FirstName).Should().Equal("Grace", "Ada");
        ranked.Last().Rank.Should().Be(2);
    }

    [Fact]
    public void ByDescending_ShouldOrderJointPositionsAlphabeticallyByFullName()
    {
        var ranked = RankNamed(("Zoe", "Zeta", 90), ("Ada", "Lovelace", 90), ("Grace", "Hopper", 90));

        ranked.Select(r => r.Item.FirstName).Should().Equal("Ada", "Grace", "Zoe");
        ranked.Select(r => r.Rank).Should().Equal(1, 1, 1);
    }

    [Fact]
    public void ByDescending_ShouldOrderJointPositionsBySurname_WhenFirstNamesMatch()
    {
        // The reason the tie-break is the full name and not the displayed "Ada L": both of these players
        // render identically, so the abbreviated form cannot separate them.
        var ranked = RankNamed(("Ada", "Lovelace", 90), ("Ada", "Lamarr", 90));

        ranked.Select(r => r.Item.LastName).Should().Equal("Lamarr", "Lovelace");
    }

    [Fact]
    public void ByDescending_ShouldNotLetTheAlphabeticalOrderChangeAnyPosition()
    {
        // Alphabetical order settles who prints first among equals. It must never promote anyone: the player
        // on the higher score is first whatever their name.
        var ranked = RankNamed(("Zoe", "Zeta", 100), ("Ada", "Lovelace", 90));

        ranked.Select(r => r.Item.FirstName).Should().Equal("Zoe", "Ada");
        ranked.Select(r => r.Rank).Should().Equal(1, 2);
    }

    [Fact]
    public void ByDescending_ShouldIgnoreCaseWhenOrderingJointPositions()
    {
        var ranked = RankNamed(("zoe", "zeta", 90), ("Ada", "Lovelace", 90));

        ranked.Select(r => r.Item.FirstName).Should().Equal("Ada", "zoe");
    }

    [Fact]
    public void ByDescending_ShouldRankByAnyComparableScore_NotOnlyIntegers()
    {
        // Prize funds and averages are decimals, so the rule is generic over the score type.
        var players = new[] { ("Ada", 1.5m), ("Grace", 2.5m), ("Alan", 1.5m) };

        var ranked = Ranking.ByDescending(players, p => p.Item2, p => p.Item1);

        ranked.Select(r => r.Item.Item1).Should().Equal("Grace", "Ada", "Alan");
        ranked.Select(r => r.Rank).Should().Equal(1, 2, 2);
    }

    // Surnames default to a fixed value so score-only tests are unaffected by the tie-break; the tests that
    // exercise ordering supply their own.
    private static IReadOnlyList<Ranked<Player>> Rank(params (string Name, int Points)[] players) =>
        RankNamed(players.Select(p => (p.Name, "Zzz", p.Points)).ToArray());

    private static IReadOnlyList<Ranked<Player>> RankNamed(params (string First, string Last, int Points)[] players) =>
        Ranking.ByDescending(
            players.Select(p => new Player(p.First, p.Last, p.Points)),
            p => p.Points,
            p => PlayerDisplayName.FormatFull(p.FirstName, p.LastName));
}
