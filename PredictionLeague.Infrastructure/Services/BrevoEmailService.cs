using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.Services;

namespace PredictionLeague.Infrastructure.Services;

public class BrevoEmailService : IEmailService
{
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly BrevoSettings _settings;

    public BrevoEmailService(IOptions<BrevoSettings> settings, ILogger<BrevoEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task SendHtmlEmailAsync(string to, string subject, string htmlContent)
    {
        var sendSmtpEmail = GetBaseEmail(to);

        sendSmtpEmail.Subject = subject;
        sendSmtpEmail.HtmlContent = htmlContent;

        await SendEmailAsync(sendSmtpEmail);
    }

    public async System.Threading.Tasks.Task SendTemplatedEmailAsync(string to, long templateId, object parameters)
    {
        var sendSmtpEmail = GetBaseEmail(to);

        sendSmtpEmail.TemplateId = templateId;
        sendSmtpEmail.Params = parameters;

        await SendEmailAsync(sendSmtpEmail);
    }

    private TransactionalEmailsApi GetApiInstance()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("Brevo API Key is not configured.");

        var apiInstance = new TransactionalEmailsApi();
        apiInstance.Configuration.ApiKey["api-key"] = _settings.ApiKey;

        return apiInstance;
    }

    private SendSmtpEmail GetBaseEmail(string to)
    {
        if (string.IsNullOrWhiteSpace(_settings.SendFromName))
            throw new InvalidOperationException("Brevo Send From Name is not configured");

        if (string.IsNullOrWhiteSpace(_settings.SendFromEmail))
            throw new InvalidOperationException("Brevo Send From Email is not configured");

        var sender = new SendSmtpEmailSender(_settings.SendFromName, _settings.SendFromEmail);
        var toList = new List<SendSmtpEmailTo> { new(to) };

        var email = new SendSmtpEmail(
            sender: sender,
            to: toList
        );

        return email;
    }

    private async System.Threading.Tasks.Task SendEmailAsync(SendSmtpEmail sendSmtpEmail)
    {
        try
        {
            var apiInstance = GetApiInstance();

            var result = await apiInstance.SendTransacEmailAsync(sendSmtpEmail);
            _logger.LogInformation("Successfully sent email to {Email} with message ID {MessageId}", string.Join(", ", sendSmtpEmail.To.Select(t => t.Email)), result?.MessageId ?? "UNKNOWN");
        }
        catch (ApiException e)
        {
            _logger.LogError(e, "Failed to send email via Brevo. Status Code: {StatusCode}, Body: {Body}", e.ErrorCode, e.Message);
        }
    }
}