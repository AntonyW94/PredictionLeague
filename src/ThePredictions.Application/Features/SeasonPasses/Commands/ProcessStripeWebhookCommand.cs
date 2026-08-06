using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record ProcessStripeWebhookCommand(string RequestBody, string SignatureHeader) : IRequest;
