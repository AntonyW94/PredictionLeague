using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.EmailTests.Commands;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.EmailTests.Commands;

public class SendTestEmailCommandHandlerTests
{
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly SendTestEmailCommandHandler _handler;

    public SendTestEmailCommandHandlerTests()
    {
        _handler = new SendTestEmailCommandHandler(
            _userManager,
            _emailService,
            Options.Create(new BrevoSettings()),
            Options.Create(new SiteSettings()),
            Substitute.For<ILogger<SendTestEmailCommandHandler>>());
    }

    private static SendTestEmailCommand Command() =>
        new(5, new Dictionary<string, string> { ["FIRST_NAME"] = "Antony" }, "user-1");

    [Fact]
    public async Task Handle_ShouldSendToCallerAndReturnSuccess_WhenCallerHasEmail()
    {
        var caller = new ApplicationUser { Id = "user-1", Email = "antony@example.com" };
        _userManager.FindByIdAsync("user-1").Returns(caller);
        _emailService.SendTestTemplatedEmailAsync("antony@example.com", 5, Arg.Any<object>())
            .Returns(new EmailSendResult(true, "msg-123", null));

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MessageId.Should().Be("msg-123");
        result.SentTo.Should().Be("antony@example.com");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureAndNotSend_WhenCallerNotFound()
    {
        _userManager.FindByIdAsync("user-1").Returns((ApplicationUser?)null);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        await _emailService.DidNotReceiveWithAnyArgs().SendTestTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureAndNotSend_WhenCallerHasNoEmail()
    {
        _userManager.FindByIdAsync("user-1").Returns(new ApplicationUser { Id = "user-1", Email = null });

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Success.Should().BeFalse();
        await _emailService.DidNotReceiveWithAnyArgs().SendTestTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldSurfaceFailure_WhenEmailServiceReportsError()
    {
        var caller = new ApplicationUser { Id = "user-1", Email = "antony@example.com" };
        _userManager.FindByIdAsync("user-1").Returns(caller);
        _emailService.SendTestTemplatedEmailAsync("antony@example.com", 5, Arg.Any<object>())
            .Returns(new EmailSendResult(false, null, "Brevo rejected the request"));

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Brevo rejected the request");
        result.SentTo.Should().Be("antony@example.com");
    }
}
