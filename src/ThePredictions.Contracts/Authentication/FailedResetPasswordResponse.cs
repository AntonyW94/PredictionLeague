using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage]
public record FailedResetPasswordResponse(string Message) : ResetPasswordResponse(false);
