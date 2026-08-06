using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeagueBoostUsageSummaryQuery(
    int LeagueId,
    string CurrentUserId) : IRequest<List<BoostUsageSummaryDto>>;
