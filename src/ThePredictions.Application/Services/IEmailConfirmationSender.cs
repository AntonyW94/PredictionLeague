using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public interface IEmailConfirmationSender
{
    /// <summary>
    /// Issues a fresh confirmation token for the user and emails them a confirmation link.
    /// Resilient: if the email template isn't configured yet or sending fails, it logs and returns
    /// without throwing, so registration is never blocked by email delivery.
    /// </summary>
    Task SendAsync(ApplicationUser user, CancellationToken cancellationToken);
}
