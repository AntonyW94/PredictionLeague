using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record CleanupExpiredDataCommand : IRequest<CleanupResult>;

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record CleanupResult(int PasswordResetTokensDeleted);
