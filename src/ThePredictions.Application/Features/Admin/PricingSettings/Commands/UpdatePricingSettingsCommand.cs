using MediatR;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Commands;

public record UpdatePricingSettingsCommand(
    decimal BufferRate,
    decimal MinimumFloor) : IRequest;
