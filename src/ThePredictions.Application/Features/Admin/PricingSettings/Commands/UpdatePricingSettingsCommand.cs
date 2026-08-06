using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.PricingSettings.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdatePricingSettingsCommand(
    decimal BufferRate,
    decimal MinimumFloor) : IRequest;
