namespace ThePredictions.Contracts.Onboarding;

public record OnboardingStepDto(
    string Key,
    string Title,
    bool Required,
    bool Skippable,
    string State,        // Completed | Active | Locked | Skipped
    string ActionLabel,
    string ActionHref);
