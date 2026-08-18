using System.Reflection;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Everything the suite needs from its environment, read once so a misconfiguration reads the same way
/// everywhere. Nothing here is a real credential: the stack is created empty, per run, and destroyed, so
/// the seeded password and signing secret are literals on purpose. There is nothing to leak.
/// </summary>
internal static class E2ESettings
{
    private const string PortVariable = "E2E_PORT";
    private const string WebAppDllVariable = "E2E_WEB_APP_DLL";
    private const string HeadedVariable = "E2E_HEADED";
    private const string SlowMoVariable = "E2E_SLOW_MO_MS";
    private const string ArtifactsVariable = "E2E_ARTIFACTS_DIR";

    /// <summary>
    /// The seeded player. Not a dev-site account: this user exists only in the throwaway database this
    /// run creates.
    /// </summary>
    internal const string PlayerEmail = "player@e2e.test";

    internal const string PlayerFirstName = "Ellie";
    internal const string PlayerLastName = "Tester";

    /// <summary>
    /// Hashed into the seeded row, then typed into the real login form. Identity's password rules only
    /// run at registration, so this needs to satisfy nothing but our own readability.
    /// </summary>
    internal const string PlayerPassword = "e2e-not-a-real-password";

    /// <summary>
    /// HS256 rejects a key under 128 bits, and <c>appsettings.json</c> leaves the real one as an
    /// unresolved <c>${Jwt-Secret}</c> placeholder when no Key Vault is configured - 13 characters, which
    /// would throw the moment a token was signed. Hence a long, obviously-fake literal.
    /// </summary>
    internal const string JwtSecret = "e2e-tests-only-not-a-secret-0000000000000000";

    /// <summary>
    /// AES-GCM key for the encrypted payout fields. Must be valid base64 decoding to exactly 32 bytes, which
    /// <c>FieldEncryptionService</c>'s constructor enforces - and it is a singleton in the handler graph behind
    /// several dashboard reads, so an invalid key takes those pages down rather than just the payout screen.
    /// Thirty-two zero bytes: obviously not a real key, for a database that lives for half a minute.
    /// </summary>
    internal const string FieldEncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    /// <summary>
    /// The environment name the application is launched under. Chosen because there is deliberately no
    /// <c>appsettings.E2E.json</c>: only the base file loads, so no <c>KeyVaultUri</c> is present and the
    /// Key Vault branch in <c>Program.cs</c> is skipped entirely. It is also not Development or Local, so
    /// <c>UseWebAssemblyDebugging</c> stays off.
    /// </summary>
    internal const string EnvironmentName = "E2E";

    /// <summary>
    /// Blazor WebAssembly has a runtime to download and start before it renders anything, and the very
    /// first page load of a run also pays the app's own cold start. Generous on purpose.
    /// </summary>
    internal const float NavigationTimeoutMs = 90_000;

    /// <summary>
    /// The ceiling for a web-first assertion, applied in <see cref="StackFixture"/> - see the note there on why
    /// setting it is a separate call from the context timeout.
    /// </summary>
    /// <remarks>
    /// A ceiling, not a cost: an assertion returns the moment its condition holds, so this is only ever paid by
    /// one that genuinely fails. Kept generous on that basis. The suite is six journeys and a test stops at its
    /// first failed assertion, so even a wholly broken suite pays about two and a half extra minutes, against a
    /// false red that costs somebody an investigation and a re-run. Five seconds - the built-in default this
    /// spent its whole life running at - was already under a real 5.1-second join response.
    /// </remarks>
    internal const float AssertionTimeoutMs = 30_000;

    /// <summary>How long to wait for the launched application to answer its liveness endpoint.</summary>
    internal static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    internal static int Port =>
        int.TryParse(Read(PortVariable), out var port) ? port : 5099;

    internal static string BaseUrl => $"http://localhost:{Port}";

    /// <summary>
    /// The published application to launch. Publishing is not done here on purpose - it is a build step,
    /// so a compile error is a build failure rather than a mysterious test timeout.
    /// </summary>
    internal static string WebAppDll =>
        Read(WebAppDllVariable)
        ?? Path.Combine(RepositoryRoot, "artifacts", "e2e-web", "ThePredictions.Web.dll");

    internal static bool RunHeaded =>
        bool.TryParse(Read(HeadedVariable), out var headed) && headed;

    internal static float SlowMoMs =>
        float.TryParse(Read(SlowMoVariable), out var slowMo) ? slowMo : 0;

    internal static string ArtifactsDirectory =>
        Read(ArtifactsVariable) ?? Path.Combine(AppContext.BaseDirectory, "e2e-artifacts");

    internal static string RepositoryRoot { get; } = ResolveRepositoryRoot();

    private static string ResolveRepositoryRoot()
    {
        var root = typeof(E2ESettings).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == "RepositoryRoot")
            ?.Value;

        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("The RepositoryRoot assembly metadata is missing; see this project's csproj.");

        return Path.GetFullPath(root);
    }

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
