using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetAvailableBoostsQuery(int LeagueId, int RoundId, string UserId) : IRequest<List<BoostOptionDto>>;
