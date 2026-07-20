using MediatR;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public record ProcessStripeWebhookCommand(string RequestBody, string SignatureHeader) : IRequest;
