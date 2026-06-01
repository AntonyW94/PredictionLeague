using MediatR;
using ThePredictions.Application.Repositories;
using DomainPricingSettings = ThePredictions.Domain.Models.PricingSettings;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Commands;

public class UpdatePricingSettingsCommandHandler(IPricingSettingsRepository pricingSettingsRepository)
    : IRequestHandler<UpdatePricingSettingsCommand>
{
    public async Task Handle(UpdatePricingSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await pricingSettingsRepository.GetAsync(cancellationToken);

        if (settings is null)
        {
            // No row seeded yet - create one from the defaults, then apply the requested values.
            settings = DomainPricingSettings.CreateDefault();
            settings.Update(request.BufferRate, request.MinimumFloor);
            await pricingSettingsRepository.AddAsync(settings, cancellationToken);
            return;
        }

        settings.Update(request.BufferRate, request.MinimumFloor);
        await pricingSettingsRepository.UpdateAsync(settings, cancellationToken);
    }
}
