using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Services.Payments;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public class ProcessStripeWebhookCommandHandler(
    IPaymentService paymentService,
    IMediator mediator,
    ILogger<ProcessStripeWebhookCommandHandler> logger) : IRequestHandler<ProcessStripeWebhookCommand>
{
    public async Task Handle(ProcessStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.RequestBody);
        Guard.Against.NullOrWhiteSpace(request.SignatureHeader);

        // Signature is verified inside the payment service; a completed checkout returns fulfilment
        // details, any other event type returns null. Fulfilment (webhook, server-to-server) is the
        // authoritative path, never the browser success redirect.
        var payment = paymentService.ParseCheckoutCompletedEvent(request.RequestBody, request.SignatureHeader);
        if (payment is null)
        {
            logger.LogInformation("Received a Stripe webhook event that is not a completed checkout; ignoring.");
            return;
        }

        await mediator.Send(
            new FulfilSeasonPassCommand(payment.UserId, payment.SeasonId, payment.Tier, payment.AmountPaid, payment.SmsFeePaid, payment.PaymentReference),
            cancellationToken);
    }
}
