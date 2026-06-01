using MediatR;
using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetLeaguePayoutsQuery(int LeagueId, string RequestingUserId) : IRequest<LeaguePayoutsDto>;
