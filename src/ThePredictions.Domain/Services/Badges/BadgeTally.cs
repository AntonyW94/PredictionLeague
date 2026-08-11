namespace ThePredictions.Domain.Services.Badges;

/// <summary>
/// What a player has collected, as one comparable score: how many different badges, and when they last earned
/// one. Bigger is better, so this is what the badges leaderboard ranks on.
/// </summary>
/// <remarks>
/// The second half is the interesting part. Two players on the same number of badges are not level: the one who
/// got there first is ahead, so an <b>earlier</b> date is the better tally. Folding that into the score rather
/// than leaving it as a sort order is what lets <see cref="Ranking"/> award positions on it - two players level
/// on both parts share a position, exactly as they would on any other leaderboard in the application.
///
/// This settles a disagreement. The leaderboard page ordered by count and date and then numbered the rows
/// one-by-one, so players who were genuinely level were shown different positions decided by their names. The
/// dashboard tile worked the same player's position out in SQL as "one more than the players ahead of me", which
/// does let them share. On our own data nine players hold no badges at all and five hold eighteen apiece with
/// the same date, so the two screens disagreed about most of the table.
/// </remarks>
public readonly record struct BadgeTally(int BadgeCount, DateTime? LastAwardedUtc) : IComparable<BadgeTally>
{
    public int CompareTo(BadgeTally other)
    {
        var byCount = BadgeCount.CompareTo(other.BadgeCount);

        if (byCount != 0)
            return byCount;

        // Reversed, because sooner is better. A player holding no badges has no date at all; treating that as the
        // end of time leaves them level with everyone else who holds none, rather than ahead of them.
        return (other.LastAwardedUtc ?? DateTime.MaxValue).CompareTo(LastAwardedUtc ?? DateTime.MaxValue);
    }
}
