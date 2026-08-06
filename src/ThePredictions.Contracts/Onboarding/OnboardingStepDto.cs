using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Onboarding;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record OnboardingStepDto(
    string Key,
    string Title,
    bool Required,
    bool Skippable,
    string State,        // Completed | Active | Locked | Skipped
    string ActionLabel,
    string ActionHref);
