using FluentAssertions;
using MediatR;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

public class GetPrizePreviewByCodeQueryHandlerTests
{
    private readonly IPrizeEvaluationInputsReader _reader = Substitute.For<IPrizeEvaluationInputsReader>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));
    private readonly GetPrizePreviewByCodeQueryHandler _handler;

    public GetPrizePreviewByCodeQueryHandlerTests()
    {
        _handler = new GetPrizePreviewByCodeQueryHandler(_reader, new PrizeEvaluator(), _dateTimeProvider, _mediator);
    }

    private PrizeEvaluationInputs Inputs(decimal entryCost = 13m, int entrants = 16) => new()
    {
        LeagueName = "Test League",
        AdministratorName = "Antony W",
        AdministratorUserId = "admin",
        EntryCode = "8G6T4N",
        EntryCost = entryCost,
        EntrantCount = entrants,
        EntryDeadlineUtc = _dateTimeProvider.UtcNow.AddDays(7),
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
    public async Task Handle_ShouldReturnPreviewWithDelta_WhenCodeResolvesToLeague()
    {
        _reader.LoadByEntryCodeAsync("8G6T4N", Arg.Any<CancellationToken>()).Returns(Inputs());

        var preview = await _handler.Handle(new GetPrizePreviewByCodeQuery("8G6T4N", "user-1"), CancellationToken.None);

        preview.LeagueName.Should().Be("Test League");
        preview.CurrentPrizePot.Should().Be(13 * 16);
        preview.ProjectedPrizePot.Should().Be(13 * 17);
        preview.HasPrizes.Should().BeTrue();
        preview.Attribution.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenCodeMatchesNoLeague()
    {
        _reader.LoadByEntryCodeAsync("NOPE12", Arg.Any<CancellationToken>()).Returns((PrizeEvaluationInputs?)null);

        var act = () => _handler.Handle(new GetPrizePreviewByCodeQuery("NOPE12", "user-1"), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
