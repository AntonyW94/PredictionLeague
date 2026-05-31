namespace ThePredictions.Contracts.Onboarding;

public record OnboardingChecklistDto(
    bool RequiredComplete,   // all required steps done -> dashboard exits "takeover" mode
    bool HasOutstandingSteps, // any non-completed, non-skipped step remains -> show the checklist
    IReadOnlyList<OnboardingStepDto> Steps);
