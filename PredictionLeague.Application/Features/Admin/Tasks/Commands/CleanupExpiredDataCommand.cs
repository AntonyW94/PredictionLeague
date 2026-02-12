using MediatR;

namespace PredictionLeague.Application.Features.Admin.Tasks.Commands;

public record CleanupExpiredDataCommand : IRequest<CleanupResult>;

public record CleanupResult(int PasswordResetTokensDeleted);
