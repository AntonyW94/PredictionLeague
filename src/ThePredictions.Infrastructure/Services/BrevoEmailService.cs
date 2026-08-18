using System.Diagnostics.CodeAnalysis;
using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

[ExcludeFromCodeCoverage(Justification = "Third-party API client: a thin call into an external SDK, verified against the live service.")]
public class BrevoEmailService(
    IOptions<BrevoSettings> settings,
    IOptions<TimeoutSettings> timeoutSettings,
    IOptions<EmailDeliverySettings> deliverySettings,
    IEmailSettingsProvider emailSettingsProvider,
    ILogger<BrevoEmailService> logger) : IEmailService
{
    private readonly BrevoSettings _settings = settings.Value;
    private readonly EmailDeliverySettings _deliverySettings = deliverySettings.Value;
    private readonly int _timeoutMilliseconds = timeoutSettings.Value.EmailServiceTimeoutSeconds * 1000;

    public async System.Threading.Tasks.Task SendTemplatedEmailAsync(string to, long templateId, object parameters)
    {
        // Master switch (database-backed, admin-toggleable). When off, automated emails are silently suppressed.
        // The explicit admin email-test path (SendTestTemplatedEmailAsync) deliberately bypasses this.
        if (!await emailSettingsProvider.AreEmailsEnabledAsync(CancellationToken.None))
        {
            logger.LogInformation("Email delivery is disabled; suppressing email to {Email} (Template ID: {TemplateId}).", to, templateId);
            return;
        }

        // Environment-specific allow-list (e.g. dev). When configured, only listed addresses receive mail.
        if (!IsRecipientAllowed(to))
        {
            logger.LogInformation("Recipient {Email} is not in the configured allow-list; suppressing email (Template ID: {TemplateId}).", to, templateId);
            return;
        }

        var sendSmtpEmail = GetBaseEmail(to);

        sendSmtpEmail.TemplateId = templateId;
        sendSmtpEmail.Params = parameters;

        await SendEmailAsync(sendSmtpEmail);
    }

    private bool IsRecipientAllowed(string to)
    {
        var allowed = _deliverySettings.AllowedRecipients;
        if (allowed is null || allowed.Length == 0)
            return true;

        return allowed.Any(address => string.Equals(address, to, StringComparison.OrdinalIgnoreCase));
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
        catch (InvalidOperationException e)
        {
            // Brevo is not configured: no API key, no sender name, no sender address. That is a deployment
            // fault rather than a send failure, but it arrives here by the same route and must not be allowed
            // to take down whatever the email was about. An invalid key was already survivable - it comes back
            // as a 401 ApiException, caught above - and a blank one used to escape and fail the caller instead,
            // which is the harsher outcome for the same underlying mistake.
            logger.LogError(e, "Failed to send email via Brevo: the service is not configured");
        }
    }
}
