using System.Text.RegularExpressions;
using brevo_csharp.Api;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

public partial class BrevoEmailTemplateCatalog(
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

            var paramNames = ExtractParamNames(template.HtmlContent);
            result.Add(new EmailTemplateInfo(
                template.Id.Value,
                template.Name ?? string.Empty,
                template.Subject ?? string.Empty,
                template.IsActive ?? false,
                paramNames));
        }

        return result.OrderBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Extracts every distinct <c>{{ params.X }}</c> merge-tag name from a template's HTML,
    /// preserving first-seen order so the test-tool form lists inputs in document order.
    /// </summary>
    private static IReadOnlyList<string> ExtractParamNames(string? htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent))
            return [];

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ParamTagRegex().Matches(htmlContent))
        {
            var name = match.Groups[1].Value;
            if (seen.Add(name))
                names.Add(name);
        }

        return names;
    }

    [GeneratedRegex(@"\{\{\s*params\.([A-Za-z0-9_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ParamTagRegex();
}
