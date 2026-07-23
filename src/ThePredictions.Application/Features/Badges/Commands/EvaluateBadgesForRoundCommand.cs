using MediatR;

namespace ThePredictions.Application.Features.Badges.Commands;

/// <summary>
/// Evaluates and awards all badges earned as of a completed round. Idempotent - safe to re-run and safe
/// to replay over history during the backfill.
/// </summary>
public record EvaluateBadgesForRoundCommand(int RoundId) : IRequest;
