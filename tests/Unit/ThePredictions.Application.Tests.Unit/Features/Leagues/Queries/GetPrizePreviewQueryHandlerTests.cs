using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

public class GetPrizePreviewQueryHandlerTests
{
    private readonly IPrizeEvaluationInputsReader _reader = Substitute.For<IPrizeEvaluationInputsReader>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));
    private readonly GetPrizePreviewQueryHandler _handler;

    public GetPrizePreviewQueryHandlerTests()
    {
        _handler = new GetPrizePreviewQueryHandler(_reader, new PrizeEvaluator(), _dateTimeProvider);
    }

    private PrizeEvaluationInputs Inputs(string? entryCode = null, decimal entryCost = 13m, int entrants = 16, DateTime? deadline = null) => new()
    {
        LeagueName = "Test League",
        AdministratorName = "Antony W",
        AdministratorUserId = "admin",
        EntryCode = entryCode,
        EntryCost = entryCost,
        EntrantCount = entrants,
        EntryDeadlineUtc = deadline ?? _dateTimeProvider.UtcNow.AddDays(7),
        NumberOfRounds = 38,
        NumberOfMonths = 9,
        HasScheme = true,
        AdminTopUpPounds = 0,
        Categories = new[]
        {
            new PrizeSchemeCategoryInput { Category = PrizeType.Overall, PerEntryPounds = 8 },
            new PrizeSchemeCategoryInput { Category = PrizeType.Round, PerEntryPounds = 3 },
            new PrizeSchemeCategoryInput { Category = PrizeType.MostExactScores, PerEntryPounds = 2 }
        }
    };

    [Fact]
    public async Task Handle_ShouldReturnPreviewWithDelta_ForPublicLeagueBeforeDeadline()
    {
        _reader.LoadAsync(1, Arg.Any<CancellationToken>()).Returns(Inputs());

        var preview = await _handler.Handle(new GetPrizePreviewQuery(1, null), CancellationToken.None);

        preview.CurrentPrizePot.Should().Be(13 * 16);
        preview.ProjectedPrizePot.Should().Be(13 * 17);
        preview.HasPrizes.Should().BeTrue();
        preview.DeadlinePassed.Should().BeFalse();
        preview.Attribution.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenPrivateLeagueAndWrongCode()
    {
        _reader.LoadAsync(1, Arg.Any<CancellationToken>()).Returns(Inputs(entryCode: "SECRET"));

        var act = () => _handler.Handle(new GetPrizePreviewQuery(1, "WRONG"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldAllowPrivateLeague_WhenCodeMatchesCaseInsensitively()
    {
        _reader.LoadAsync(1, Arg.Any<CancellationToken>()).Returns(Inputs(entryCode: "SECRET"));

        var preview = await _handler.Handle(new GetPrizePreviewQuery(1, "secret"), CancellationToken.None);

        preview.HasPrizes.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldShowFrozenBreakdownWithoutDelta_WhenDeadlinePassed()
    {
        _reader.LoadAsync(1, Arg.Any<CancellationToken>()).Returns(Inputs(deadline: _dateTimeProvider.UtcNow.AddDays(-1)));

        var preview = await _handler.Handle(new GetPrizePreviewQuery(1, null), CancellationToken.None);

        preview.DeadlinePassed.Should().BeTrue();
        preview.Attribution.Should().BeEmpty();
        preview.Breakdown.Categories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnNoPrizes_WhenFreeLeagueWithoutTopUp()
    {
        var inputs = Inputs(entryCost: 0m);
        _reader.LoadAsync(1, Arg.Any<CancellationToken>()).Returns(inputs);

        var preview = await _handler.Handle(new GetPrizePreviewQuery(1, null), CancellationToken.None);

        preview.HasPrizes.Should().BeFalse();
        preview.Breakdown.Categories.Should().BeEmpty();
    }
}
