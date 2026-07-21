using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Features.SeasonPasses.Commands;

namespace ThePredictions.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/stripe")]
public class StripeWebhookController(IMediator mediator, ILogger<StripeWebhookController> logger) : ControllerBase
{
    private const string SignatureHeaderName = "Stripe-Signature";

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var requestBody = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers[SignatureHeaderName].ToString();

        try
        {
            await mediator.Send(new ProcessStripeWebhookCommand(requestBody, signature), cancellationToken);
            return Ok();
        }
        catch (PaymentWebhookSignatureException ex)
        {
            logger.LogWarning("Rejected a Stripe webhook with an invalid signature: {Message}", ex.Message);
            return BadRequest();
        }
    }
}
