using brevo_csharp.Api;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

public class BrevoEmailTemplateCatalog(
    IOptions<BrevoSettings> settings,
    IOptions<TimeoutSettings> timeoutSettings,
    IMemoryCache cache,
    ILogger<BrevoEmailTemplateCatalog> logger) : IEmailTemplateCatalog
{
    private const string CacheKey = "brevo-email-templates";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly BrevoSettings _settings = settings.Value;
    private readonly int _timeoutMilliseconds = timeoutSettings.Value.EmailServiceTimeoutSeconds * 1000;

    public async Task<IReadOnlyList<EmailTemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<EmailTemplateInfo>? cached) && cached is not null)
            return cached;

        var templates = await FetchTemplatesAsync();
        cache.Set(CacheKey, templates, CacheDuration);

        logger.LogInformation("Discovered {TemplateCount} Brevo email templates", templates.Count);
        return templates;
    }

    private async Task<IReadOnlyList<EmailTemplateInfo>> FetchTemplatesAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("Brevo API Key is not configured.");

        var apiInstance = new TransactionalEmailsApi();
        apiInstance.Configuration.ApiKey["api-key"] = _settings.ApiKey;
        apiInstance.Configuration.Timeout = _timeoutMilliseconds;

        var response = await apiInstance.GetSmtpTemplatesAsync(templateStatus: null, limit: 1000, offset: 0);
        if (response?.Templates is null)
            return [];

        var result = new List<EmailTemplateInfo>();
        foreach (var template in response.Templates)
        {
            if (template.Id is null)
                continue;

            var paramNames = EmailTemplateParameters.Extract(template.HtmlContent);
            result.Add(new EmailTemplateInfo(
                template.Id.Value,
                template.Name ?? string.Empty,
                template.Subject ?? string.Empty,
                template.IsActive ?? false,
                paramNames));
        }

        return result.OrderBy(t => t.Name).ToList();
    }
}
