using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

public class BrevoEmailService(IOptions<BrevoSettings> settings, IOptions<TimeoutSettings> timeoutSettings, ILogger<BrevoEmailService> logger) : IEmailService
{
    private readonly BrevoSettings _settings = settings.Value;
    private readonly int _timeoutMilliseconds = timeoutSettings.Value.EmailServiceTimeoutSeconds * 1000;

    public async System.Threading.Tasks.Task SendTemplatedEmailAsync(string to, long templateId, object parameters)
    {
        var sendSmtpEmail = GetBaseEmail(to);

        sendSmtpEmail.TemplateId = templateId;
        sendSmtpEmail.Params = parameters;

        await SendEmailAsync(sendSmtpEmail);
    }

    public async Task<EmailSendResult> SendTestTemplatedEmailAsync(string to, long templateId, object parameters)
    {
        var sendSmtpEmail = GetBaseEmail(to);

        sendSmtpEmail.TemplateId = templateId;
        sendSmtpEmail.Params = parameters;

        try
        {
            var apiInstance = GetApiInstance();

            var result = await apiInstance.SendTransacEmailAsync(sendSmtpEmail);
            var messageId = result?.MessageId ?? "UNKNOWN";

            logger.LogInformation("Successfully sent test email to {Email} with message ID {MessageId}", to, messageId);
            return new EmailSendResult(true, messageId, null);
        }
        catch (ApiException e)
        {
            logger.LogError(e, "Failed to send test email via Brevo. Status Code: {StatusCode}, Body: {Body}", e.ErrorCode, e.Message);
            return new EmailSendResult(false, null, e.Message);
        }
    }

    private TransactionalEmailsApi GetApiInstance()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("Brevo API Key is not configured.");

        var apiInstance = new TransactionalEmailsApi();
        apiInstance.Configuration.ApiKey["api-key"] = _settings.ApiKey;
        apiInstance.Configuration.Timeout = _timeoutMilliseconds;

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
            logger.LogInformation("Successfully sent email to {Email} with message ID {MessageId}", string.Join(", ", sendSmtpEmail.To.Select(t => t.Email)), result?.MessageId ?? "UNKNOWN");
        }
        catch (ApiException e)
        {
            logger.LogError(e, "Failed to send email via Brevo. Status Code: {StatusCode}, Body: {Body}", e.ErrorCode, e.Message);
        }
    }
}