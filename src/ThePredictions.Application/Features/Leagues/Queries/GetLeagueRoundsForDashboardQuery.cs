using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Rounds;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeagueRoundsForDashboardQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<RoundDto>>;
