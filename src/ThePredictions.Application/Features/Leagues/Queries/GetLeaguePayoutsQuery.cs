using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeaguePayoutsQuery(int LeagueId, string RequestingUserId) : IRequest<LeaguePayoutsDto>;
