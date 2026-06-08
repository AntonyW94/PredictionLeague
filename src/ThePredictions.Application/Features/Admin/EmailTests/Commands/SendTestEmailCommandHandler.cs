using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Commands;

public class SendTestEmailCommandHandler(
    IUserManager userManager,
    IEmailService emailService,
    ILogger<SendTestEmailCommandHandler> logger)
    : IRequestHandler<SendTestEmailCommand, SendTestEmailResultDto>
{
    public async Task<SendTestEmailResultDto> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        var caller = await userManager.FindByIdAsync(request.CallerUserId);

        if (caller is null || string.IsNullOrWhiteSpace(caller.Email))
        {
            logger.LogWarning("Test email requested but caller (ID: {UserId}) has no email address", request.CallerUserId);
            return new SendTestEmailResultDto(false, null, "Could not determine your email address to send the test to.", string.Empty);
        }

        var result = await emailService.SendTestTemplatedEmailAsync(caller.Email, request.TemplateId, request.Parameters);

        logger.LogInformation(
            "Test email for template (ID: {TemplateId}) sent to {Email}: success={Success}",
            request.TemplateId,
            caller.Email,
            result.Success);

        return new SendTestEmailResultDto(result.Success, result.MessageId, result.Error, caller.Email);
    }
}
