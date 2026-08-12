using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Onboarding;
using ThePredictions.Application.Features.Onboarding.Queries;
using ThePredictions.Contracts.Onboarding;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Onboarding.Queries;

/// <summary>
/// The checklist that gets a new player started. The steps themselves belong to the registry; what this handler decides is
/// whether each one has been done - and the only one of those with any judgement in it is the mobile number.
/// </summary>
public class GetOnboardingChecklistQueryHandlerTests
{
    private const string UserId = "user-me";

    private readonly IOnboardingStateQuery _query = Substitute.For<IOnboardingStateQuery>();
    private readonly GetOnboardingChecklistQueryHandler _handler;

    public GetOnboardingChecklistQueryHandlerTests()
    {
        _handler = new GetOnboardingChecklistQueryHandler(_query);
        GivenSkippedSteps();
    }

    [Fact]
    public async Task Handle_ShouldReturnAChecklist_ForABrandNewAccount()
    {
        // Arrange
        Given(new OnboardingStateRow(PassCount: 0, LeagueCount: 0, PhoneNumber: null, HasPayoutDetails: false));

        // Act
        var checklist = await HandleAsync();

        // Assert
        checklist.Steps.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldNotCountABlankMobileNumber(string? phoneNumber)
    {
        // A number made only of spaces is not a mobile number. The column allows one, and the step must not tick itself for
        // somebody who saved a blank - this was LEN(LTRIM(RTRIM(...))) > 0 inside the statement.
        Given(new OnboardingStateRow(0, 0, phoneNumber, false));

        // Act
        var checklist = await HandleAsync();

        // Assert
        StepIsDone(checklist, MobileStepKey()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCountARealMobileNumber()
    {
        // Arrange
        Given(new OnboardingStateRow(0, 0, "07700900000", false));

        // Act
        var checklist = await HandleAsync();

        // Assert
        StepIsDone(checklist, MobileStepKey()).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCountAPassAndALeagueAndBankDetails()
    {
        // Arrange
        Given(new OnboardingStateRow(PassCount: 1, LeagueCount: 1, PhoneNumber: "07700900000", HasPayoutDetails: true));

        // Act
        var checklist = await HandleAsync();

        // Assert - with everything done there is nothing left to prompt for.
        checklist.RequiredComplete.Should().BeTrue();
        checklist.HasOutstandingSteps.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPassTheSkippedStepsThrough()
    {
        // A step the player has dismissed is marked as such rather than kept nagging them, which the registry applies.
        Given(new OnboardingStateRow(0, 0, null, false));
        GivenSkippedSteps(MobileStepKey());

        // Act
        var checklist = await HandleAsync();

        // Assert
        checklist.Steps.Single(step => step.Key == MobileStepKey()).State.Should().Be("Skipped");
    }

    [Fact]
    public async Task Handle_ShouldAskAboutThePlayerRequested()
    {
        // Arrange
        Given(new OnboardingStateRow(0, 0, null, false));

        // Act
        await HandleAsync();

        // Assert
        await _query.Received(1).ExecuteAsync(UserId, Arg.Any<CancellationToken>());
        await _query.Received(1).GetSkippedStepKeysAsync(UserId, Arg.Any<CancellationToken>());
    }

    private void Given(OnboardingStateRow row) =>
        _query.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(row);

    private void GivenSkippedSteps(params string[] stepKeys) =>
        _query.GetSkippedStepKeysAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(stepKeys);

    /// <summary>The step the mobile number ticks off. Named by the registry, so the test asks it rather than guessing.</summary>
    private static string MobileStepKey() => OnboardingStepKeys.AddMobile;

    private static bool StepIsDone(OnboardingChecklistDto checklist, string stepKey) =>
        checklist.Steps.Single(step => step.Key == stepKey).State == "Completed";

    private Task<OnboardingChecklistDto> HandleAsync() =>
        _handler.Handle(new GetOnboardingChecklistQuery(UserId), CancellationToken.None);
}
