using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Features.Badges;

namespace ThePredictions.Application.Features.Badges.Commands;

/// <summary>
/// Evaluates and awards all badges earned as of a completed round. Idempotent - safe to re-run and safe
/// to replay over history during the backfill. Returns the badges that were genuinely newly awarded by
/// this run (a real insert, not an idempotent no-op), so the round-results digest can celebrate them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record EvaluateBadgesForRoundCommand(int RoundId) : IRequest<IReadOnlyList<RoundBadgeAward>>;
