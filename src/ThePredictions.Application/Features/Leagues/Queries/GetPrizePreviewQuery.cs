using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The prospective-member prize preview: headline facts, the projected breakdown if they join, and
/// the attributed "+£x" effect of their own entry. For private leagues the entry code must match.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetPrizePreviewQuery(int LeagueId, string? EntryCode) : IRequest<PrizePreviewDto>;
