namespace ThePredictions.Application.Services;

public interface IEmailService
{
    Task SendTemplatedEmailAsync(string to, long templateId, object parameters);

    /// <summary>
    /// Sends a templated email and reports the outcome (Brevo message ID on success, error on
    /// failure) instead of swallowing errors. Used by the admin email-test tool.
    /// </summary>
    Task<EmailSendResult> SendTestTemplatedEmailAsync(string to, long templateId, object parameters);
}