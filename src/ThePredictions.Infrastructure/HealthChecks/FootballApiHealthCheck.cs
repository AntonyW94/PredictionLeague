using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;

namespace ThePredictions.Infrastructure.HealthChecks;

[ExcludeFromCodeCoverage(Justification = "Health check: endpoint wiring and a live dependency probe, verified by hitting the endpoint.")]
public class FootballApiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly FootballApiSettings _settings;
    private readonly ILogger<FootballApiHealthCheck> _logger;

    public FootballApiHealthCheck(
        HttpClient httpClient,
        IOptions<FootballApiSettings> settings,
        ILogger<FootballApiHealthCheck> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.BaseUrl))
            {
                return HealthCheckResult.Degraded("Football API base URL is not configured.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_settings.BaseUrl), "status"));
            request.Headers.Add("x-apisports-key", _settings.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Football API is reachable.");
            }

            _logger.LogWarning("Football API health check returned status code {StatusCode}", response.StatusCode);
            return HealthCheckResult.Degraded($"Football API returned status code {response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Football API health check failed");
            return HealthCheckResult.Unhealthy("Football API is unreachable.", ex);
        }
    }
}
