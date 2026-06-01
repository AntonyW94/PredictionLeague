using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.PricingSettings;
using DomainPricingSettings = ThePredictions.Domain.Models.PricingSettings;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

public class GetPricingSettingsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetPricingSettingsQuery, PricingSettingsDto>
{
    public async Task<PricingSettingsDto> Handle(GetPricingSettingsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1
                ps.[BufferRate],
                ps.[MinimumFloor]
            FROM
                [PricingSettings] ps
            ORDER BY
                ps.[Id];";

        var settings = await dbConnection.QuerySingleOrDefaultAsync<PricingSettingsDto>(sql, cancellationToken);

        // Fall back to the built-in defaults if no row has been seeded yet.
        if (settings is not null)
            return settings;

        var defaults = DomainPricingSettings.CreateDefault();
        return new PricingSettingsDto(defaults.BufferRate, defaults.MinimumFloor);
    }
}
