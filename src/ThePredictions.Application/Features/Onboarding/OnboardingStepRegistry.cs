using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Application.Features.Onboarding;

/// <summary>
/// The onboarding steps are defined here in code. The database only stores per-user *skips*
/// (by these stable string keys); completion is always derived from live data. Adding a new
/// step = add an entry here - every user (incl. existing) picks it up automatically with no
/// migration, because it isn't in their skips and its completion is computed fresh.
/// </summary>
public static class OnboardingStepRegistry
{
    /// <summary>Optional steps a user may skip (and that "Dismiss" skips in bulk).</summary>
    public static readonly IReadOnlyList<string> OptionalKeys = new[] { OnboardingStepKeys.AddMobile, OnboardingStepKeys.AddPayoutDetails };

    public static OnboardingChecklistDto Build(OnboardingUserState state, ISet<string> skippedKeys)
    {
        var getPassDone = state.PassCount > 0;
        var joinLeagueDone = state.LeagueCount > 0;
        var addMobileDone = state.HasMobile;
        var addPayoutDetailsDone = state.HasPayoutDetails;

        var definitions = new[]
        {
            new StepDefinition(OnboardingStepKeys.GetPass, "Get your Season Pass", Required: true, Skippable: false,
                Completed: getPassDone, PrerequisiteMet: true, "Get pass", "/season-passes"),
            new StepDefinition(OnboardingStepKeys.JoinLeague, "Join or create a league", Required: true, Skippable: false,
                Completed: joinLeagueDone, PrerequisiteMet: getPassDone, "Find a league", "/dashboard"),
            new StepDefinition(OnboardingStepKeys.AddMobile, "Complete your profile", Required: false, Skippable: true,
                Completed: addMobileDone, PrerequisiteMet: true, "Complete", "/account/details"),
            new StepDefinition(OnboardingStepKeys.AddPayoutDetails, "Add payout details", Required: false, Skippable: true,
                Completed: addPayoutDetailsDone, PrerequisiteMet: true, "Add details", "/account/payout-details")
        };

        var steps = definitions.Select(definition => ToDto(definition, skippedKeys)).ToList();

        var requiredComplete = definitions.Where(d => d.Required).All(d => d.Completed);
        var hasOutstanding = steps.Any(s => s.State is OnboardingStepStates.Active or OnboardingStepStates.Locked);

        return new OnboardingChecklistDto(requiredComplete, hasOutstanding, steps);
    }

    private static OnboardingStepDto ToDto(StepDefinition definition, ISet<string> skippedKeys)
    {
        string state;
        if (definition.Completed)
            state = OnboardingStepStates.Completed;
        else if (skippedKeys.Contains(definition.Key))
            state = OnboardingStepStates.Skipped;
        else if (!definition.PrerequisiteMet)
            state = OnboardingStepStates.Locked;
        else
            state = OnboardingStepStates.Active;

        return new OnboardingStepDto(definition.Key, definition.Title, definition.Required, definition.Skippable, state, definition.ActionLabel, definition.ActionHref);
    }

    private record StepDefinition(
        string Key,
        string Title,
        bool Required,
        bool Skippable,
        bool Completed,
        bool PrerequisiteMet,
        string ActionLabel,
        string ActionHref);
}
