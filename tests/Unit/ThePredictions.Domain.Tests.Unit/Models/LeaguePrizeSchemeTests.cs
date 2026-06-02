using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeaguePrizeSchemeTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));

    private static IEnumerable<LeaguePrizeSchemeEntry> SeasonEntries() => new[]
    {
        LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8),
        LeaguePrizeSchemeEntry.Create(PrizeType.Round, 3),
        LeaguePrizeSchemeEntry.Create(PrizeType.MostExactScores, 2)
    };

    [Fact]
    public void Create_ShouldCreateScheme_WhenValid()
    {
        var scheme = LeaguePrizeScheme.Create(13, SeasonEntries(), "admin-user", isTournament: false, _dateTimeProvider);

        scheme.SetByUserId.Should().Be("admin-user");
        scheme.SetAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        scheme.Entries.Should().HaveCount(3);
    }

    [Fact]
    public void Create_ShouldAllowFreeLeague_WhenAllAllocationsZero()
    {
        var entries = new[]
        {
            LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 0),
            LeaguePrizeSchemeEntry.Create(PrizeType.MostExactScores, 0)
        };

        var scheme = LeaguePrizeScheme.Create(0, entries, "admin-user", isTournament: false, _dateTimeProvider);
        scheme.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void Create_ShouldThrow_WhenStakeNegative()
    {
        var act = () => LeaguePrizeScheme.Create(-1, SeasonEntries(), "admin-user", false, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenSetByUserIdBlank()
    {
        var act = () => LeaguePrizeScheme.Create(13, SeasonEntries(), "  ", false, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenEntriesNull()
    {
        var act = () => LeaguePrizeScheme.Create(13, null!, "admin-user", false, _dateTimeProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNoEntries()
    {
        var act = () => LeaguePrizeScheme.Create(0, Array.Empty<LeaguePrizeSchemeEntry>(), "admin-user", false, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenDuplicateCategory()
    {
        var entries = new[]
        {
            LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 6),
            LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 7)
        };

        var act = () => LeaguePrizeScheme.Create(13, entries, "admin-user", false, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenAllocationsDoNotSumToStake()
    {
        var act = () => LeaguePrizeScheme.Create(20, SeasonEntries(), "admin-user", false, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenSectionEnabledForNonTournament()
    {
        var entries = new[]
        {
            LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8),
            LeaguePrizeSchemeEntry.Create(PrizeType.Stages, 5)
        };

        var act = () => LeaguePrizeScheme.Create(13, entries, "admin-user", isTournament: false, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenMonthlyEnabledForTournament()
    {
        var entries = new[]
        {
            LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8),
            LeaguePrizeSchemeEntry.Create(PrizeType.Monthly, 5)
        };

        var act = () => LeaguePrizeScheme.Create(13, entries, "admin-user", isTournament: true, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldAllowSection_ForTournament()
    {
        var entries = new[]
        {
            LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 8),
            LeaguePrizeSchemeEntry.Create(PrizeType.Stages, 5)
        };

        var scheme = LeaguePrizeScheme.Create(13, entries, "admin-user", isTournament: true, _dateTimeProvider);
        scheme.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void AssignToLeague_ShouldSetForeignKey()
    {
        var scheme = LeaguePrizeScheme.Create(13, SeasonEntries(), "admin-user", false, _dateTimeProvider);
        scheme.AssignToLeague(7);
        scheme.LeagueId.Should().Be(7);
    }

    [Fact]
    public void AssignToLeague_ShouldThrow_WhenIdZero()
    {
        var scheme = LeaguePrizeScheme.Create(13, SeasonEntries(), "admin-user", false, _dateTimeProvider);
        var act = () => scheme.AssignToLeague(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HydrationConstructor_ShouldSetAllFields_AndFilterNullEntries()
    {
        var entries = new LeaguePrizeSchemeEntry?[]
        {
            new(1, 9, PrizeType.Overall, 8, null),
            null
        };

        var scheme = new LeaguePrizeScheme(9, 3, _dateTimeProvider.UtcNow, "admin-user", entries);

        scheme.Id.Should().Be(9);
        scheme.LeagueId.Should().Be(3);
        scheme.SetByUserId.Should().Be("admin-user");
        scheme.Entries.Should().ContainSingle();
    }

    [Fact]
    public void HydrationConstructor_ShouldHandleNullEntries()
    {
        var scheme = new LeaguePrizeScheme(9, 3, _dateTimeProvider.UtcNow, "admin-user", null);
        scheme.Entries.Should().BeEmpty();
    }
}
