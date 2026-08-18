using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Onboarding;

/// <summary>The states an onboarding step can be in, shared by the server registry and every reader of it.</summary>
/// <remarks>
/// <see cref="OnboardingStepDto.State"/> is a string rather than an enum so the wire format stays readable, which left
/// the four values as literals written out in the registry and compared as literals by anything counting them. The
/// administrator's user list counts completed steps, so there are now two places that have to agree.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public static class OnboardingStepStates
{
    /// <summary>The data says the step is done, whatever the account has clicked.</summary>
    public const string Completed = "Completed";

    /// <summary>Outstanding, and they can do it now.</summary>
    public const string Active = "Active";

    /// <summary>Outstanding, but an earlier step blocks it.</summary>
    public const string Locked = "Locked";

    /// <summary>They dismissed it. Only an optional step can be skipped.</summary>
    public const string Skipped = "Skipped";
}
