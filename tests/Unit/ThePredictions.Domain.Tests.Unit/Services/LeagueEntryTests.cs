using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Whether a league is still accepting entries.
///
/// Both league-discovery queries wrote this as <c>EntryDeadlineUtc &gt; GETUTCDATE()</c>, which did two things at once: the
/// comparison, and - because <c>NULL &gt; anything</c> is unknown in SQL - the silent exclusion of a league with no
/// deadline at all.
/// </summary>
public class LeagueEntryTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsOpen_ShouldBeTrue_WhileTheDeadlineIsAhead()
    {
        LeagueEntry.IsOpen(Now.AddHours(1), Now).Should().BeTrue();
    }

    [Fact]
    public void IsOpen_ShouldBeFalse_OnceTheDeadlineHasPassed()
    {
        LeagueEntry.IsOpen(Now.AddHours(-1), Now).Should().BeFalse();
    }

    [Fact]
    public void IsOpen_ShouldBeFalse_AtTheDeadlineItself()
    {
        // The old comparison was strictly greater than, and stays that way.
        LeagueEntry.IsOpen(Now, Now).Should().BeFalse();
    }

    [Fact]
    public void IsOpen_ShouldBeFalse_ForALeagueWithNoDeadlineAtAll()
    {
        // The rule nobody wrote down: in SQL this fell out of three-valued logic, so a league with no deadline was never
        // offered to anybody. Stated here so it survives being read by someone who does not think in SQL nulls.
        LeagueEntry.IsOpen(null, Now).Should().BeFalse();
    }
}
