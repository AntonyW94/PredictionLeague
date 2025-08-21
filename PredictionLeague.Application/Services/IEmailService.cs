namespace PredictionLeague.Application.Services;

public interface IEmailService
{
    Task SendHtmlEmailAsync(string to, string subject, string htmlContent);
    Task SendTemplatedEmailAsync(string to, long templateId, object parameters);
}