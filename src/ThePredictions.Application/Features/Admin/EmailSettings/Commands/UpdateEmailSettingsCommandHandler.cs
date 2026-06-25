using MediatR;
using ThePredictions.Application.Repositories;
using DomainEmailSettings = ThePredictions.Domain.Models.EmailSettings;

namespace ThePredictions.Application.Features.Admin.EmailSettings.Commands;

public class UpdateEmailSettingsCommandHandler(IEmailSettingsRepository emailSettingsRepository)
    : IRequestHandler<UpdateEmailSettingsCommand>
{
    public async Task Handle(UpdateEmailSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await emailSettingsRepository.GetAsync(cancellationToken);

        if (settings is null)
        {
            // No row seeded yet - create one from the default, then apply the requested value.
            settings = DomainEmailSettings.CreateDefault();
            settings.Update(request.EmailsEnabled);
            await emailSettingsRepository.AddAsync(settings, cancellationToken);
            return;
        }

        settings.Update(request.EmailsEnabled);
        await emailSettingsRepository.UpdateAsync(settings, cancellationToken);
    }
}
