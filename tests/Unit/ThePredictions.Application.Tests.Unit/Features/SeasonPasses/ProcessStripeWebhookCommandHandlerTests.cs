using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Features.SeasonPasses.Commands;
using ThePredictions.Application.Services.Payments;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.SeasonPasses;

public class ProcessStripeWebhookCommandHandlerTests
{
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ProcessStripeWebhookCommandHandler _handler;

    private const string Body = "{\"id\":\"evt_1\"}";
    private const string Signature = "t=1,v1=abc";

    public ProcessStripeWebhookCommandHandlerTests()
    {
        _handler = new ProcessStripeWebhookCommandHandler(_paymentService, _mediator, NullLogger<ProcessStripeWebhookCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldFulfilPass_WhenCheckoutCompleted()
    {
        var completion = new PaymentCheckoutCompletion("user-123", 7, SeasonPassTier.Standard, 10m, 0m, "pi_test_123");
        _paymentService.ParseCheckoutCompletedEvent(Body, Signature).Returns(completion);

        await _handler.Handle(new ProcessStripeWebhookCommand(Body, Signature), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<FulfilSeasonPassCommand>(c => c.UserId == "user-123" && c.SeasonId == 7
                && c.Tier == SeasonPassTier.Standard && c.AmountPaid == 10m && c.SmsFeePaid == 0m
                && c.PaymentReference == "pi_test_123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldIgnoreEvent_WhenNotCheckoutCompleted()
    {
        _paymentService.ParseCheckoutCompletedEvent(Body, Signature).Returns((PaymentCheckoutCompletion?)null);

        await _handler.Handle(new ProcessStripeWebhookCommand(Body, Signature), CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<FulfilSeasonPassCommand>(), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldPropagate_WhenSignatureInvalid()
    {
        _paymentService.ParseCheckoutCompletedEvent(Body, Signature).Returns(_ => throw new PaymentWebhookSignatureException("bad signature"));

        var act = () => _handler.Handle(new ProcessStripeWebhookCommand(Body, Signature), CancellationToken.None);

        await act.Should().ThrowAsync<PaymentWebhookSignatureException>();
        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<FulfilSeasonPassCommand>(), CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_ShouldThrow_WhenBodyMissing(string body)
    {
        var act = () => _handler.Handle(new ProcessStripeWebhookCommand(body, Signature), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_ShouldThrow_WhenSignatureMissing(string signature)
    {
        var act = () => _handler.Handle(new ProcessStripeWebhookCommand(Body, signature), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
