using FluentAssertions;
using ThePredictions.Application.Features.Onboarding;
using ThePredictions.Contracts.Onboarding;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Onboarding;

public class OnboardingStepRegistryTests
{
    private static OnboardingUserState State(
        int passCount = 0,
        int leagueCount = 0,
        bool hasMobile = false,
        bool hasPayoutDetails = false) =>
        new(passCount, leagueCount, hasMobile, hasPayoutDetails);

    private static OnboardingChecklistDto Build(OnboardingUserState state, params string[] skipped) =>
        OnboardingStepRegistry.Build(state, new HashSet<string>(skipped));

    private static OnboardingStepDto Step(OnboardingChecklistDto checklist, string key) =>
        checklist.Steps.Single(s => s.Key == key);

    [Fact]
    public void Build_ShouldReturnEveryStep()
    {
        var checklist = Build(State());

        checklist.Steps.Select(s => s.Key).Should().BeEquivalentTo([
            OnboardingStepKeys.GetPass,
            OnboardingStepKeys.JoinLeague,
            OnboardingStepKeys.AddMobile,
            OnboardingStepKeys.AddPayoutDetails
        ]);
    }

    [Fact]
    public void Build_ShouldStartANewUserWithGetPassActiveAndJoinLeagueLocked()
    {
        var checklist = Build(State());

        Step(checklist, OnboardingStepKeys.GetPass).State.Should().Be("Active");
        Step(checklist, OnboardingStepKeys.JoinLeague).State.Should().Be("Locked");
    }

    [Fact]
    public void Build_ShouldUnlockJoinLeague_OnceAPassIsHeld()
    {
        var checklist = Build(State(passCount: 1));

        Step(checklist, OnboardingStepKeys.GetPass).State.Should().Be("Completed");
        Step(checklist, OnboardingStepKeys.JoinLeague).State.Should().Be("Active");
    }

    [Fact]
    public void Build_ShouldCompleteJoinLeague_OnceALeagueIsJoined()
    {
        var checklist = Build(State(passCount: 1, leagueCount: 1));

        Step(checklist, OnboardingStepKeys.JoinLeague).State.Should().Be("Completed");
    }

    [Fact]
    public void Build_ShouldCompleteTheProfileStep_WhenAMobileIsPresent()
    {
        Step(Build(State(hasMobile: true)), OnboardingStepKeys.AddMobile).State.Should().Be("Completed");
    }

    [Fact]
    public void Build_ShouldCompleteThePayoutStep_WhenDetailsArePresent()
    {
        Step(Build(State(hasPayoutDetails: true)), OnboardingStepKeys.AddPayoutDetails).State.Should().Be("Completed");
    }

    [Fact]
    public void Build_ShouldMarkASkippedStepAsSkipped()
    {
        var checklist = Build(State(), OnboardingStepKeys.AddMobile);

        Step(checklist, OnboardingStepKeys.AddMobile).State.Should().Be("Skipped");
    }

    [Fact]
    public void Build_ShouldPreferCompletedOverSkipped()
    {
        // Someone who skipped the step and then did it anyway has completed it.
        var checklist = Build(State(hasMobile: true), OnboardingStepKeys.AddMobile);

        Step(checklist, OnboardingStepKeys.AddMobile).State.Should().Be("Completed");
    }

    [Fact]
    public void Build_ShouldPreferSkippedOverLocked()
    {
        var checklist = Build(State(), OnboardingStepKeys.JoinLeague);

        Step(checklist, OnboardingStepKeys.JoinLeague).State.Should().Be("Skipped");
    }

    [Fact]
    public void Build_ShouldReportRequiredIncomplete_UntilBothRequiredStepsAreDone()
    {
        Build(State()).RequiredComplete.Should().BeFalse();
        Build(State(passCount: 1)).RequiredComplete.Should().BeFalse();
        Build(State(passCount: 1, leagueCount: 1)).RequiredComplete.Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldIgnoreOptionalSteps_WhenDecidingRequiredComplete()
    {
        var checklist = Build(State(passCount: 1, leagueCount: 1));

        checklist.RequiredComplete.Should().BeTrue();
        Step(checklist, OnboardingStepKeys.AddMobile).State.Should().Be("Active");
    }

    [Fact]
    public void Build_ShouldReportOutstandingWork_WhileAnyStepIsActiveOrLocked()
    {
        Build(State()).HasOutstandingSteps.Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldReportNothingOutstanding_WhenEveryStepIsDone()
    {
        var checklist = Build(State(passCount: 1, leagueCount: 1, hasMobile: true, hasPayoutDetails: true));

        checklist.HasOutstandingSteps.Should().BeFalse();
        checklist.RequiredComplete.Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldReportNothingOutstanding_WhenTheRemainingStepsAreSkipped()
    {
        var checklist = Build(
            State(passCount: 1, leagueCount: 1),
            OnboardingStepKeys.AddMobile,
            OnboardingStepKeys.AddPayoutDetails);

        checklist.HasOutstandingSteps.Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldMarkOnlyTheOptionalStepsAsSkippable()
    {
        var checklist = Build(State());

        checklist.Steps.Where(s => s.Skippable).Select(s => s.Key)
            .Should().BeEquivalentTo(OnboardingStepRegistry.OptionalKeys);
    }

    [Fact]
    public void Build_ShouldMarkOnlyThePassAndLeagueStepsAsRequired()
    {
        var checklist = Build(State());

        checklist.Steps.Where(s => s.Required).Select(s => s.Key)
            .Should().BeEquivalentTo([OnboardingStepKeys.GetPass, OnboardingStepKeys.JoinLeague]);
    }

    [Fact]
    public void Build_ShouldGiveEveryStepAnActionLabelAndLink()
    {
        var checklist = Build(State());

        checklist.Steps.Should().OnlyContain(s =>
            !string.IsNullOrWhiteSpace(s.ActionLabel) && s.ActionHref.StartsWith("/"));
    }
}
