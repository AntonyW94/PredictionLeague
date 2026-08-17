using System.Collections.Concurrent;
using System.Diagnostics;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Runs the published <c>ThePredictions.Web</c> against the throwaway database, as its own process, for the
/// duration of the test run.
/// </summary>
/// <remarks>
/// The <b>published</b> output rather than <c>dotnet run</c>: publishing materialises the Blazor client into
/// wwwroot and runs the CSS bundling target, so what the browser is handed is what a deployment hands it.
/// <c>dotnet run</c> from source would depend on static web assets, which only resolve automatically in the
/// Development environment - and Development is exactly the environment this cannot use, because
/// <c>Program.cs</c> then demands an <c>appsettings.Development.Secrets.json</c> for Key Vault.
///
/// The site is <b>not</b> containerised, and deliberately. A CI runner is already an ephemeral environment
/// that is destroyed after the run, so a Dockerfile would add an image to maintain and a build per run for
/// no isolation that is not already there.
/// </remarks>
internal sealed class WebApplicationProcess : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _output = new();

    private Process? _process;

    internal async Task StartAsync(string connectionString)
    {
        var dll = E2ESettings.WebAppDll;

        if (!File.Exists(dll))
            throw new InvalidOperationException(
                $"The published application is not at '{dll}'. Publish it first:{Environment.NewLine}"
                + $"    dotnet publish src/ThePredictions.Web -c Release -o artifacts/e2e-web{Environment.NewLine}"
                + "or point E2E_WEB_APP_DLL at an existing publish.");

        _process = Start(dll, connectionString);

        await WaitUntilLiveAsync();
    }

    /// <summary>
    /// Writes everything the application logged to the artifacts folder.
    /// </summary>
    /// <remarks>
    /// Without this, a failing journey tells you the page showed an error and never why: the panel says
    /// "Could not load your leagues", and the exception that caused it lives in the application's console
    /// output, which was captured and then discarded. The Playwright trace does not help either - it records
    /// the browser's side, so a 500 is a 500. This is the server's half of the same story, and it is written
    /// on every run because the run that needs it is the one that already failed.
    /// </remarks>
    internal async Task WriteLogAsync()
    {
        if (_output.IsEmpty)
            return;

        Directory.CreateDirectory(E2ESettings.ArtifactsDirectory);

        await File.WriteAllLinesAsync(
            Path.Combine(E2ESettings.ArtifactsDirectory, "application.log"), _output);
    }

    public async ValueTask DisposeAsync()
    {
        await WriteLogAsync();

        if (_process is null)
            return;

        if (!_process.HasExited)
        {
            // The tree, not just the process: `dotnet <dll>` can leave the host behind otherwise, and a
            // survivor holds the port against the next run.
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
        _process = null;
    }

    private Process Start(string dll, string connectionString)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            // The publish folder, so appsettings.json is found and the DataProtection key ring is written
            // somewhere disposable rather than into the repository.
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // An environment with no appsettings file of its own, so only the base file loads: no KeyVaultUri,
        // so Program.cs skips Key Vault entirely and never asks for a secrets file.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = E2ESettings.EnvironmentName;
        startInfo.Environment["ASPNETCORE_URLS"] = E2ESettings.BaseUrl;

        startInfo.Environment["ConnectionStrings__DataConnection"] = connectionString;

        // appsettings.json restricts AllowedHosts to the two production hostnames, which would make the
        // host filtering middleware reject every request to localhost with a 400.
        startInfo.Environment["AllowedHosts"] = "*";

        // Drives CORS, SiteSettings.BaseUrl, and - through ${ApiBaseUrl} substitution - the JWT issuer and
        // audience, so it has to be the origin the browser actually uses.
        startInfo.Environment["ApiBaseUrl"] = E2ESettings.BaseUrl;

        // Without this the signing key stays the unresolved ${Jwt-Secret} placeholder, which is under the
        // 128 bits HS256 demands, and the first sign-in throws instead of returning a token.
        startInfo.Environment["JwtSettings__Secret"] = E2ESettings.JwtSecret;

        // FieldEncryptionService's CONSTRUCTOR rejects a key that is not valid base64, and it is a singleton
        // injected into the handler graph behind My Leagues, Active Rounds and Standings. Left unresolved the
        // placeholder ${FieldEncryption-Key} is not base64, so all three dashboard reads returned 500 and the
        // page rendered three error panels - which looked convincingly like an application bug until the
        // application's own log said otherwise.
        startInfo.Environment["FieldEncryption__Key"] = E2ESettings.FieldEncryptionKey;

        // appsettings.json sets Serilog's default minimum to Warning, which suppresses request logging - the
        // category is Serilog's own, not ThePredictions, so the Information override does not reach it. A
        // production choice, and the wrong one for a stack whose whole job is to be diagnosed: without this,
        // a read that fails without throwing leaves no trace of its status code anywhere.
        startInfo.Environment["Serilog__MinimumLevel__Default"] = "Information";

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) => Capture(e.Data);
        process.ErrorDataReceived += (_, e) => Capture(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    /// <summary>
    /// Polls the liveness endpoint until the application answers.
    /// </summary>
    /// <remarks>
    /// <c>/health/live</c> rather than <c>/health/ready</c>: ready probes the database <i>and</i> the
    /// football API, and there is no football API key here, so a ready check would never pass. That the
    /// database works is proved by signing in, which is the point of the journey anyway.
    /// </remarks>
    private async Task WaitUntilLiveAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        var deadline = DateTime.UtcNow + E2ESettings.StartupTimeout;

        while (DateTime.UtcNow < deadline)
        {
            // Checked first and every time round: a configuration or migration failure exits in seconds,
            // and waiting out the full timeout to then say "it never became live" hides the actual reason.
            if (_process!.HasExited)
                throw new InvalidOperationException(Report($"exited early with code {_process.ExitCode}"));

            try
            {
                var response = await client.GetAsync($"{E2ESettings.BaseUrl}/health/live");

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException)
            {
                // Listening but not answering yet.
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new InvalidOperationException(Report($"did not answer {E2ESettings.BaseUrl}/health/live "
                                                   + $"within {E2ESettings.StartupTimeout.TotalSeconds:0} seconds"));
    }

    private void Capture(string? line)
    {
        if (line is not null)
            _output.Enqueue(line);
    }

    /// <summary>
    /// The application's own output, attached to the failure. Without it a startup problem surfaces as a
    /// timeout with nothing to read, which is the least useful failure a harness can produce.
    /// </summary>
    private string Report(string what)
    {
        var captured = _output.ToArray();

        var detail = captured.Length == 0
            ? "It produced no output at all, which usually means the dotnet host itself could not start."
            : string.Join(Environment.NewLine, captured);

        return $"The application under test {what}.{Environment.NewLine}{Environment.NewLine}"
               + $"--- its output ---{Environment.NewLine}{detail}";
    }
}
