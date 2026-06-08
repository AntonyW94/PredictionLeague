namespace ThePredictions.Application.Services;

/// <summary>
/// Outcome of a single email send where the caller needs to know whether it succeeded
/// (unlike the fire-and-forget <see cref="IEmailService.SendTemplatedEmailAsync"/>).
/// </summary>
public record EmailSendResult(bool Success, string? MessageId, string? Error);
