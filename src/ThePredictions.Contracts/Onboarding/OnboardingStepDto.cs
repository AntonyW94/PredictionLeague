using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Onboarding;

[ExcludeFromCodeCoverage]
public record OnboardingStepDto(
    string Key,
    string Title,
    bool Required,
    bool Skippable,
    string State,        // Completed | Active | Locked | Skipped
    string ActionLabel,
    string ActionHref);
