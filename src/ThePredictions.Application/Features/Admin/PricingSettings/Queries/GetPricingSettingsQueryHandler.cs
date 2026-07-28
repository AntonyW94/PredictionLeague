using System.Diagnostics.CodeAnalysis;
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

        var settings = await dbConnection.QuerySingleOrDefaultAsync<PricingSettingsQueryResult>(sql, cancellationToken);

        // Fall back to the built-in defaults if no row has been seeded yet.
        if (settings is not null)
            return new PricingSettingsDto(settings.BufferRate, settings.MinimumFloor);

        var defaults = DomainPricingSettings.CreateDefault();
        return new PricingSettingsDto(defaults.BufferRate, defaults.MinimumFloor);
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PricingSettingsQueryResult(
        decimal BufferRate,
        decimal MinimumFloor);
}
