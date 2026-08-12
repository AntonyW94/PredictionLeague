using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// What a league's prize pot currently pays out, worked out live from its scheme and the number of people who have entered.
/// </summary>
/// <remarks>
/// The membership check comes first: a prize breakdown says how much money is in a private league, so somebody who is not in it
/// must not be able to ask. After that the only decision here is whether there is a scheme to evaluate at all - a league without
/// one still has an entrant count worth showing, and evaluating nothing would divide a pot that does not exist.
/// </remarks>
public class GetLeaguePrizeBreakdownQueryHandlerTests
{
    private const int LeagueId = 7;
    private const string CurrentUserId = "user-1";

    private readonly IPrizeEvaluationInputsReader _inputsReader = Substitute.For<IPrizeEvaluationInputsReader>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly IPrizeEvaluator _evaluator = Substitute.For<IPrizeEvaluator>();
    private readonly GetLeaguePrizeBreakdownQueryHandler _handler;

    public GetLeaguePrizeBreakdownQueryHandlerTests()
    {
        _handler = new GetLeaguePrizeBreakdownQueryHandler(_inputsReader, _membershipService, _evaluator);
    }

    #region Who may ask

    [Fact]
    public async Task Handle_ShouldCheckTheCallerIsInTheLeague()
    {
        // Arrange
        GivenInputs(Inputs(hasScheme: false));

        // Act
        await HandleAsync();

        // Assert
        await _membershipService.Received(1).EnsureApprovedMemberAsync(LeagueId, CurrentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotReadTheLeague_WhenTheCallerIsNotInIt()
    {
        // The check has to come first, or a refused caller still learns the pot exists from how long the answer takes.
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, CurrentUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var act = () => HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _inputsReader.DidNotReceiveWithAnyArgs().LoadAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReportTheLeagueIsMissing_WhenThereIsNoSuchLeague()
    {
        // Arrange
        _inputsReader.LoadAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((PrizeEvaluationInputs?)null);

        // Act
        var act = () => HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region A league with no prize scheme

    [Fact]
    public async Task Handle_ShouldReturnJustTheEntrantCount_WhenTheLeagueHasNoScheme()
    {
        // Arrange
        GivenInputs(Inputs(hasScheme: false, entrantCount: 12));

        // Act
        var breakdown = await HandleAsync();

        // Assert
        breakdown.EntrantCount.Should().Be(12);
    }

    [Fact]
    public async Task Handle_ShouldNotWorkOutAPot_WhenTheLeagueHasNoScheme()
    {
        // Arrange - there is nothing to divide, and evaluating an empty scheme would produce a breakdown of zeroes that
        // reads as a real one.
        GivenInputs(Inputs(hasScheme: false));

        // Act
        await HandleAsync();

        // Assert
        _evaluator.DidNotReceiveWithAnyArgs().Evaluate(default!);
    }

    #endregion

    #region A league with a prize scheme

    [Fact]
    public async Task Handle_ShouldReturnTheWorkedOutBreakdown_WhenTheLeagueHasAScheme()
    {
        // Arrange
        var expected = new PrizeBreakdownDto { EntrantCount = 12 };
        GivenInputs(Inputs(hasScheme: true));
        _evaluator.Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>()).Returns(expected);

        // Act
        var breakdown = await HandleAsync();

        // Assert
        breakdown.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_ShouldWorkThePotOutAtTheNumberOfPeopleActuallyEntered()
    {
        // Arrange - the pot grows with entries, so the count the page shows and the count it divides by must be the same one.
        GivenInputs(Inputs(hasScheme: true, entrantCount: 12, entryCost: 10m));
        _evaluator.Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>()).Returns(new PrizeBreakdownDto());

        // Act
        await HandleAsync();

        // Assert
        _evaluator.Received(1).Evaluate(Arg.Is<PrizeSchemeEvaluationRequest>(request =>
            request.EntrantCount == 12 && request.StakePounds == 10));
    }

    #endregion

    private static PrizeEvaluationInputs Inputs(bool hasScheme, int entrantCount = 8, decimal entryCost = 10m) =>
        new()
        {
            LeagueId = LeagueId,
            LeagueName = "The Office",
            EntrantCount = entrantCount,
            EntryCost = entryCost,
            NumberOfRounds = 38,
            NumberOfMonths = 10,
            HasScheme = hasScheme
        };

    private void GivenInputs(PrizeEvaluationInputs inputs) =>
        _inputsReader.LoadAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(inputs);

    private Task<PrizeBreakdownDto> HandleAsync() =>
        _handler.Handle(new GetLeaguePrizeBreakdownQuery(LeagueId, CurrentUserId), CancellationToken.None);
}
