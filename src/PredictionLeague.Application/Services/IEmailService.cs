namespace PredictionLeague.Application.Services;

public interface IEmailService
{
    Task SendTemplatedEmailAsync(string to, long templateId, object parameters);
}