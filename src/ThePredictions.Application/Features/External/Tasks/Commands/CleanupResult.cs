using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record CleanupResult(int PasswordResetTokensDeleted);
