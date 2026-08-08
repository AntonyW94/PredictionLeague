using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

/// <summary>
/// The prize preview a prospective member sees before joining. Once the deadline has passed there
/// is no "if you join" split to show, and a league with nothing in the pot shows no prizes at all
/// rather than an empty breakdown.
/// </summary>
public class PrizePreviewComposerTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly IPrizeEvaluator _evaluator = Substitute.For<IPrizeEvaluator>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(NowUtc);

    public PrizePreviewComposerTests()
    {
        _evaluator.Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>())
            .Returns(call => new PrizeBreakdownDto
            {
                Pot = call.Arg<PrizeSchemeEvaluationRequest>().EntrantCount * 20m,
                EntrantCount = call.Arg<PrizeSchemeEvaluationRequest>().EntrantCount,
                Categories =
                [
                    new PrizeCategoryBreakdownDto
                    {
                        Category = PrizeType.Overall,
                        DisplayName = "Overall",
                        Kind = PrizeCategoryKind.EndOfSeason,
                        SubPot = call.Arg<PrizeSchemeEvaluationRequest>().EntrantCount * 20m,
                        Slots = [new PrizeSlotDto { Label = "1st", Amount = 100m, Rank = 1 }]
                    }
                ]
            });
    }

    private static PrizeEvaluationInputs Inputs(
        bool hasScheme = true,
        decimal entryCost = 20m,
        int adminTopUpPounds = 0,
        DateTime? entryDeadlineUtc = null) =>
        new()
        {
            LeagueId = 7,
            LeagueName = "The Office League",
            SeasonName = "2026/27",
            AdministratorName = "Alice A",
            AdministratorUserId = "admin-1",
            EntryCode = "ABC123",
            EntryCost = entryCost,
            EntrantCount = 12,
            EntryDeadlineUtc = entryDeadlineUtc ?? NowUtc.AddDays(7),
            NumberOfRounds = 38,
            NumberOfMonths = 10,
            HasScheme = hasScheme,
            AdminTopUpPounds = adminTopUpPounds,
            Categories = [new PrizeSchemeCategoryInput { Category = PrizeType.Overall, PerEntryPounds = 20 }]
        };

    private PrizePreviewDto Compose(PrizeEvaluationInputs inputs) =>
        PrizePreviewComposer.Compose(inputs, _evaluator, _dateTimeProvider);

    [Fact]
    public void Compose_ShouldReportTheHeadlineFacts()
    {
        var preview = Compose(Inputs());

        preview.LeagueId.Should().Be(7);
        preview.LeagueName.Should().Be("The Office League");
        preview.SeasonName.Should().Be("2026/27");
        preview.AdministratorName.Should().Be("Alice A");
        preview.EntrantCount.Should().Be(12);
    }

    [Fact]
    public void Compose_ShouldShowWhatJoiningWouldAdd_WhileTheDeadlineIsStillOpen()
    {
        var preview = Compose(Inputs());

        preview.HasPrizes.Should().BeTrue();
        preview.Attribution.Should().NotBeEmpty();
        _evaluator.Received(2).Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>());
    }

    [Fact]
    public void Compose_ShouldShowTheFinalPotWithNoJoiningSplit_OnceTheDeadlineHasPassed()
    {
        // Nobody else can join, so there is no "your entry adds" to show - only what is there now.
        var preview = Compose(Inputs(entryDeadlineUtc: NowUtc.AddDays(-1)));

        preview.HasPrizes.Should().BeTrue();
        preview.Attribution.Should().BeEmpty();
        _evaluator.Received(1).Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>());
    }

    [Fact]
    public void Compose_ShouldShowNoPrizes_WhenTheLeagueHasNoSchemeSetUp()
    {
        var preview = Compose(Inputs(hasScheme: false));

        preview.HasPrizes.Should().BeFalse();
        _evaluator.DidNotReceiveWithAnyArgs().Evaluate(default!);
    }

    [Fact]
    public void Compose_ShouldShowNoPrizes_WhenAFreeLeagueHasNoTopUpEither()
    {
        // A scheme exists but there is no money behind it, so there is nothing to show.
        var preview = Compose(Inputs(entryCost: 0m, adminTopUpPounds: 0));

        preview.HasPrizes.Should().BeFalse();
        _evaluator.DidNotReceiveWithAnyArgs().Evaluate(default!);
    }

    [Fact]
    public void Compose_ShouldShowPrizes_WhenAFreeLeagueIsFundedByTheAdministratorAlone()
    {
        var preview = Compose(Inputs(entryCost: 0m, adminTopUpPounds: 50));

        preview.HasPrizes.Should().BeTrue();
    }
}
