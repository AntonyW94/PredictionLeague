using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.PricingSettings;
using DomainPricingSettings = ThePredictions.Domain.Models.PricingSettings;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
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

        var settings = await dbConnection.QuerySingleOrDefaultAsync<PricingSettingsQueryResult>(sql, cancellationToken);

        // Fall back to the built-in defaults if no row has been seeded yet.
        if (settings is not null)
            return new PricingSettingsDto(settings.BufferRate, settings.MinimumFloor);

        var defaults = DomainPricingSettings.CreateDefault();
        return new PricingSettingsDto(defaults.BufferRate, defaults.MinimumFloor);
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PricingSettingsQueryResult(
        decimal BufferRate,
        decimal MinimumFloor);
}
