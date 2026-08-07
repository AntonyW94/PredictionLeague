using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Prizes.Queries;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;
using static ThePredictions.Application.Features.Prizes.Queries.EvaluateSchemeQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Prizes.Queries;

/// <summary>
/// The live preview an administrator sees while setting up prizes: what the pot works out at for a
/// given entry price and number of entrants, before anything is saved.
/// </summary>
public class EvaluateSchemeQueryHandlerTests
{
    private const int SeasonId = 11;

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IPrizeEvaluator _evaluator = Substitute.For<IPrizeEvaluator>();

    private readonly EvaluateSchemeQueryHandler _handler;

    public EvaluateSchemeQueryHandlerTests()
    {
        _handler = new EvaluateSchemeQueryHandler(_dbConnection, _evaluator);
        _evaluator.Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>()).Returns(new PrizeBreakdownDto { Pot = 100m });
    }

    private void GivenSeason(int numberOfRounds = 38, DateTime? startDateUtc = null, DateTime? endDateUtc = null) =>
        _dbConnection.QuerySingleOrDefaultAsync<SeasonRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new SeasonRow
            {
                NumberOfRounds = numberOfRounds,
                StartDateUtc = startDateUtc ?? SeasonStart,
                EndDateUtc = endDateUtc ?? SeasonEnd
            });

    private static PrizeSchemeRequest Scheme(params PrizeSchemeCategoryRequest[] categories) =>
        new() { Categories = categories.ToList() };

    private static PrizeSchemeCategoryRequest Category(PrizeType category, int perEntryPounds, string? rankTableJson = null) =>
        new() { Category = category, PerEntryPounds = perEntryPounds, RankTableJson = rankTableJson };

    private PrizeSchemeEvaluationRequest CapturedRequest() =>
        (PrizeSchemeEvaluationRequest)_evaluator.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IPrizeEvaluator.Evaluate))
            .GetArguments()[0]!;

    private Task<PrizeBreakdownDto> HandleAsync(
        decimal price = 20m, int entrantCount = 12, decimal? prizeFundOverride = null, PrizeSchemeRequest? scheme = null) =>
        _handler.Handle(
            new EvaluateSchemeQuery(SeasonId, price, entrantCount, scheme ?? Scheme(), prizeFundOverride),
            CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheSeasonDoesNotExist()
    {
        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnTheEvaluatorsBreakdown()
    {
        GivenSeason();

        var result = await HandleAsync();

        result.Pot.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_ShouldPassTheEntryPriceAndHeadcountThrough()
    {
        GivenSeason(numberOfRounds: 38);

        await HandleAsync(price: 20m, entrantCount: 12);

        var request = CapturedRequest();
        request.StakePounds.Should().Be(20);
        request.EntrantCount.Should().Be(12);
        request.NumberOfRounds.Should().Be(38);
    }

    public static TheoryData<decimal, int> StakeCases => new()
    {
        { 20m, 20 },
        { 0m, 0 },
        { 19.99m, 19 }
    };

    [Theory]
    [MemberData(nameof(StakeCases))]
    public async Task Handle_ShouldDropPartOfAPoundFromTheEntryPrice(decimal price, int expectedStake)
    {
        // The prize maths works in whole pounds and rounds down, so the pot can never promise more
        // than was actually collected.
        GivenSeason();

        await HandleAsync(price: price);

        CapturedRequest().StakePounds.Should().Be(expectedStake);
    }

    public static TheoryData<decimal?, int> TopUpCases => new()
    {
        { null, 0 },
        { 0m, 0 },
        { 50m, 50 },
        { 49.99m, 49 }
    };

    [Theory]
    [MemberData(nameof(TopUpCases))]
    public async Task Handle_ShouldDropPartOfAPoundFromTheAdministratorsTopUp(decimal? prizeFundOverride, int expectedTopUp)
    {
        GivenSeason();

        await HandleAsync(prizeFundOverride: prizeFundOverride);

        CapturedRequest().AdminTopUpPounds.Should().Be(expectedTopUp);
    }

    [Theory]
    [InlineData(2026, 8, 1, 2027, 5, 31, 10)]
    [InlineData(2026, 8, 1, 2026, 8, 1, 1)]
    [InlineData(2026, 8, 15, 2026, 9, 14, 1)]
    [InlineData(2026, 8, 15, 2026, 9, 15, 2)]
    [InlineData(2026, 9, 1, 2026, 8, 1, 0)]
    public async Task Handle_ShouldCountTheMonthsAMonthlyPrizeCouldBeWonIn(
        int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay, int expectedMonths)
    {
        GivenSeason(
            startDateUtc: new DateTime(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Utc),
            endDateUtc: new DateTime(endYear, endMonth, endDay, 0, 0, 0, DateTimeKind.Utc));

        await HandleAsync();

        CapturedRequest().NumberOfMonths.Should().Be(expectedMonths);
    }

    [Fact]
    public async Task Handle_ShouldPassEveryFundedCategoryThrough()
    {
        GivenSeason();

        await HandleAsync(scheme: Scheme(
            Category(PrizeType.Overall, 5, "[{\"Rank\":1,\"Share\":100}]"),
            Category(PrizeType.Monthly, 2)));

        var categories = CapturedRequest().Categories;
        categories.Should().HaveCount(2);
        categories[0].Category.Should().Be(PrizeType.Overall);
        categories[0].PerEntryPounds.Should().Be(5);
        categories[0].RankTableJson.Should().Be("[{\"Rank\":1,\"Share\":100}]");
        categories[1].Category.Should().Be(PrizeType.Monthly);
        categories[1].RankTableJson.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldStillEvaluate_WhenNoCategoryHasBeenFundedYet()
    {
        // The preview is live as the administrator types, so it has to cope with an empty scheme.
        GivenSeason();

        await HandleAsync(scheme: Scheme());

        CapturedRequest().Categories.Should().BeEmpty();
    }
}
