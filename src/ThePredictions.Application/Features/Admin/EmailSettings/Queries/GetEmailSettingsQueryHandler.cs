using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.EmailSettings;
using DomainEmailSettings = ThePredictions.Domain.Models.EmailSettings;

namespace ThePredictions.Application.Features.Admin.EmailSettings.Queries;

/// <summary>
/// The master email switch as the administrator's screen shows it.
/// </summary>
/// <remarks>
/// Reads through <see cref="IEmailSettingsQuery"/>, which already existed for the provider that caches this answer for
/// every outgoing email. This handler had its own copy of the identical statement.
/// </remarks>
public class GetEmailSettingsQueryHandler(IEmailSettingsQuery emailSettingsQuery)
    : IRequestHandler<GetEmailSettingsQuery, EmailSettingsDto>
{
    public async Task<EmailSettingsDto> Handle(GetEmailSettingsQuery request, CancellationToken cancellationToken)
    {
        var emailsEnabled = await emailSettingsQuery.GetEmailsEnabledAsync(cancellationToken);

        // No row saved yet means emails are on, which is the same rule the sending path applies.
        return new EmailSettingsDto(emailsEnabled ?? DomainEmailSettings.CreateDefault().EmailsEnabled);
    }
}
