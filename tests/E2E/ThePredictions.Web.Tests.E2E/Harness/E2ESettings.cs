using System.Globalization;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Everything the suite needs from its environment, read once so a misconfiguration reads the same way
/// everywhere. The base URL defaults to deployed dev, which is the only environment Stage 1 targets; the
/// password has no default on purpose, because a hard-coded credential is the one thing that must never
/// live in this repository.
/// </summary>
internal static class E2ESettings
{
    private const string BaseUrlVariable = "E2E_BASE_URL";
    private const string PasswordVariable = "E2E_TEST_ACCOUNT_PASSWORD";
    private const string HeadedVariable = "E2E_HEADED";
    private const string SlowMoVariable = "E2E_SLOW_MO_MS";
    private const string ArtifactsVariable = "E2E_ARTIFACTS_DIR";

    internal const string DefaultBaseUrl = "https://dev.thepredictions.co.uk";

    /// <summary>The accounts <c>TestAccountCreator</c> writes on every dev refresh.</summary>
    internal const string PlayerEmail = "testplayer@dev.local";

    internal const string AdminEmail = "testadmin@dev.local";

    /// <summary>Deliberately holds no Season Pass and no league, so it lands on the onboarding takeover.</summary>
    internal const string NewPlayerEmail = "testnewplayer@dev.local";

    /// <summary>
    /// Blazor WebAssembly has to download and start a runtime before it renders anything, and dev is on
    /// shared hosting that cold-starts, so the first paint can be tens of seconds. Waits here are
    /// deliberately generous - a browser suite that is tuned tight simply reports the hosting as a bug.
    /// </summary>
    internal const float NavigationTimeoutMs = 90_000;

    internal const float AssertionTimeoutMs = 45_000;

    internal static string BaseUrl =>
        Read(BaseUrlVariable) is { } value ? value.TrimEnd('/') : DefaultBaseUrl;

    internal static string? TestAccountPassword => Read(PasswordVariable);

    /// <summary>
    /// False when no password is configured, which is the ordinary state of a developer machine. The tests
    /// skip rather than fail in that case, so <c>dotnet test ThePredictions.sln</c> stays green locally;
    /// the workflow that is meant to run them checks the secret is present before calling dotnet, so a
    /// missing secret in CI fails loudly instead of quietly skipping the whole suite.
    /// </summary>
    internal static bool IsConfigured => TestAccountPassword is not null;

    internal static string NotConfiguredReason =>
        $"{PasswordVariable} is not set, so there is no way to sign in. Set it to the TEST_ACCOUNT_PASSWORD "
        + $"secret to run the browser suite against {BaseUrl}.";

    /// <summary>Set <c>E2E_HEADED=true</c> to watch a run locally.</summary>
    internal static bool RunHeaded =>
        bool.TryParse(Read(HeadedVariable), out var headed) && headed;

    /// <summary>Milliseconds to pause between Playwright actions. Only useful alongside <see cref="RunHeaded"/>.</summary>
    internal static float SlowMoMs =>
        float.TryParse(Read(SlowMoVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out var slowMo)
            ? slowMo
            : 0;

    /// <summary>
    /// Where per-test Playwright traces are written. Defaults inside the build output; CI points it at the
    /// workspace root so the upload step does not have to know the target framework folder.
    /// </summary>
    internal static string ArtifactsDirectory =>
        Read(ArtifactsVariable) ?? Path.Combine(AppContext.BaseDirectory, "e2e-artifacts");

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
