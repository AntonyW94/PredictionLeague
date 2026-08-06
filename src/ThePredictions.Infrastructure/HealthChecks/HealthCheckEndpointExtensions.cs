using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ThePredictions.Infrastructure.HealthChecks;

[ExcludeFromCodeCoverage(Justification = "Health check: endpoint wiring and a live dependency probe, verified by hitting the endpoint.")]
public static class HealthCheckEndpointExtensions
{
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        // Basic health endpoint — returns Healthy/Degraded/Unhealthy with minimal detail.
        // Excludes sensitive information such as exception messages and stack traces.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteMinimalResponse
        }).AllowAnonymous();

        // Liveness probe — always returns 200 if the process is running.
        // Does not execute any health checks; used by orchestrators to detect deadlocks.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteMinimalResponse
        }).AllowAnonymous();

        // Readiness probe — checks database and external dependencies.
        // Returns 503 when dependencies are unavailable so load balancers can stop sending traffic.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteMinimalResponse
        }).AllowAnonymous();

        return app;
    }

    private static async Task WriteMinimalResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var entries = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString()
        });

        var response = new
        {
            status = report.Status.ToString(),
            checks = entries
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
