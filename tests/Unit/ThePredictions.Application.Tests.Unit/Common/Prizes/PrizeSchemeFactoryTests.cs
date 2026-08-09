using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

/// <summary>
/// Turns the prize setup an administrator submits into the stored scheme. A places-table override
/// is checked here, at the moment it is set, so a broken one is rejected while the administrator is
/// still on the screen rather than surfacing when prizes are settled months later.
/// </summary>
public class PrizeSchemeFactoryTests
{
    private static readonly TestDateTimeProvider DateTimeProvider =
        new(new DateTime(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc));

    private const string ValidRankTable = "[{\"MinEntrants\":2,\"MaxEntrants\":null,\"Percentages\":[100]}]";

    private static PrizeSchemeRequest Request(params PrizeSchemeCategoryRequest[] categories) =>
        new() { Categories = categories.ToList() };

    private static PrizeSchemeCategoryRequest Category(PrizeType category, int perEntryPounds, string? rankTableJson = null) =>
        new() { Category = category, PerEntryPounds = perEntryPounds, RankTableJson = rankTableJson };

    [Fact]
    public void Build_ShouldKeepAValidPlacesTableOverride()
    {
        var scheme = PrizeSchemeFactory.Build(
            Request(Category(PrizeType.Overall, 10, ValidRankTable)),
            stakePounds: 10, setByUserId: "admin-1", isTournament: false, DateTimeProvider);

        scheme.Entries.Should().ContainSingle();
        scheme.Entries.Single().RankTableJson.Should().Be(ValidRankTable);
    }

    [Fact]
    public void Build_ShouldRejectABrokenPlacesTableOverrideAtTheMomentItIsSet()
    {
        var act = () => PrizeSchemeFactory.Build(
            Request(Category(PrizeType.Overall, 10, "not json")),
            stakePounds: 10, setByUserId: "admin-1", isTournament: false, DateTimeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_ShouldTreatAnAbsentOverrideAsUsingTheDefaultTable(string? rankTableJson)
    {
        var scheme = PrizeSchemeFactory.Build(
            Request(Category(PrizeType.Overall, 10, rankTableJson)),
            stakePounds: 10, setByUserId: "admin-1", isTournament: false, DateTimeProvider);

        scheme.Entries.Single().RankTableJson.Should().BeNull();
    }
}
