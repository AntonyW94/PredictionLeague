using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetLeagueRoundsForDashboardQuery(int LeagueId) : IRequest<IEnumerable<RoundDto>>;