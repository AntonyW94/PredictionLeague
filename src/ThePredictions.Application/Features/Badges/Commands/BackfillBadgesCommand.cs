using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Badges.Commands;

/// <summary>
/// One-off replay of the badge evaluator over every completed round in chronological order, so existing
/// players' historical badges are awarded with their real (backdated) achievement dates. Idempotent.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record BackfillBadgesCommand : IRequest;
