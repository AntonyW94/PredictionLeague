using MediatR;
using ThePredictions.Contracts.Admin.PricingSettings;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Queries;

public record GetPricingSettingsQuery : IRequest<PricingSettingsDto>;
