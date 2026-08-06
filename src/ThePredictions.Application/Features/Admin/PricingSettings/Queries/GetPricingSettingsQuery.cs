using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.PricingSettings;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetPricingSettingsQuery : IRequest<PricingSettingsDto>;
