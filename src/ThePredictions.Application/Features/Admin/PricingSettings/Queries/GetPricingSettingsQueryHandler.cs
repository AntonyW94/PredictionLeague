using MediatR;
using ThePredictions.Contracts.Admin.PricingSettings;
using DomainPricingSettings = ThePredictions.Domain.Models.PricingSettings;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

/// <summary>The stored pricing settings, or the built-in defaults if none have been saved.</summary>
public class GetPricingSettingsQueryHandler(IPricingSettingsQuery pricingSettingsQuery)
    : IRequestHandler<GetPricingSettingsQuery, PricingSettingsDto>
{
    public async Task<PricingSettingsDto> Handle(GetPricingSettingsQuery request, CancellationToken cancellationToken)
    {
        var rows = await pricingSettingsQuery.ExecuteAsync(cancellationToken);

        var settings = LivePricingSettings.From(rows);

        if (settings is null)
        {
            var defaults = DomainPricingSettings.CreateDefault();

            return new PricingSettingsDto(defaults.BufferRate, defaults.MinimumFloor);
        }

        return new PricingSettingsDto(settings.BufferRate, settings.MinimumFloor);
    }
}
