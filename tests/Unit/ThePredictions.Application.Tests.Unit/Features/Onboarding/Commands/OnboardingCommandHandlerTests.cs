using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Onboarding;
using ThePredictions.Application.Features.Onboarding.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Onboarding.Commands;

/// <summary>
/// The getting-started checklist. Some steps are optional and can be waved away; the ones that
/// actually gate taking part cannot, so a skip request for those is refused rather than ignored.
/// </summary>
public class OnboardingCommandHandlerTests
{
    private const string UserId = "user-1";

    private readonly IOnboardingSkipRepository _repository = Substitute.For<IOnboardingSkipRepository>();

    private readonly SkipOnboardingStepCommandHandler _skip;
    private readonly DismissOnboardingCommandHandler _dismiss;

    public OnboardingCommandHandlerTests()
    {
        _skip = new SkipOnboardingStepCommandHandler(_repository);
        _dismiss = new DismissOnboardingCommandHandler(_repository);
    }

    private Task SkipAsync(string stepKey, string userId = UserId) =>
        _skip.Handle(new SkipOnboardingStepCommand(userId, stepKey), CancellationToken.None);

    private Task DismissAsync(string userId = UserId) =>
        _dismiss.Handle(new DismissOnboardingCommand(userId), CancellationToken.None);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Skip_ShouldRefuseAnEmptyUser(string? userId)
    {
        var act = () => SkipAsync(OnboardingStepKeys.AddMobile, userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Skip_ShouldRefuseAnEmptyStep(string? stepKey)
    {
        var act = () => SkipAsync(stepKey!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(OnboardingStepKeys.AddMobile)]
    [InlineData(OnboardingStepKeys.AddPayoutDetails)]
    public async Task Skip_ShouldWaveAwayAnOptionalStep(string stepKey)
    {
        await SkipAsync(stepKey);

        await _repository.Received(1).AddSkipsAsync(
            UserId, Arg.Is<IEnumerable<string>>(k => k.Single() == stepKey), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OnboardingStepKeys.GetPass)]
    [InlineData(OnboardingStepKeys.JoinLeague)]
    public async Task Skip_ShouldRefuseAStepThatGatesTakingPart(string stepKey)
    {
        // Getting a pass and joining a league are what the site is for - skipping them would leave
        // the checklist saying nothing is left to do while the account still cannot play.
        var act = () => SkipAsync(stepKey);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage($"*'{stepKey}' cannot be skipped*");
        await _repository.DidNotReceiveWithAnyArgs().AddSkipsAsync(default!, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Skip_ShouldRefuseAStepThatDoesNotExist()
    {
        var act = () => SkipAsync("not-a-real-step");

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Dismiss_ShouldRefuseAnEmptyUser(string? userId)
    {
        var act = () => DismissAsync(userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Dismiss_ShouldWaveAwayEveryOptionalStepAtOnce()
    {
        // Dismissing the checklist is the same as skipping each optional step, so a step added
        // later is not covered and the checklist quietly reappears for it.
        await DismissAsync();

        await _repository.Received(1).AddSkipsAsync(
            UserId,
            Arg.Is<IEnumerable<string>>(k => k.SequenceEqual(OnboardingStepRegistry.OptionalKeys)),
            Arg.Any<CancellationToken>());
    }
}
