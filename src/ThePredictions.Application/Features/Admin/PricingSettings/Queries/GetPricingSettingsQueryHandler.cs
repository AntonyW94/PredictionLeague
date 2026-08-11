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

        // The earliest row is the live one. This is a single-row table by convention rather than by constraint, so which
        // row wins if a second ever appears is a decision - it was TOP 1 ORDER BY [Id] in SQL.
        var settings = rows.MinBy(row => row.Id);

        if (settings is null)
        {
            var defaults = DomainPricingSettings.CreateDefault();

            return new PricingSettingsDto(defaults.BufferRate, defaults.MinimumFloor);
        }

        return new PricingSettingsDto(settings.BufferRate, settings.MinimumFloor);
    }
}
