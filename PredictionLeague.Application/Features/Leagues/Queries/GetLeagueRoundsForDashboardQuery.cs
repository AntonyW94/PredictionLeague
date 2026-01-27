using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetLeagueRoundsForDashboardQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<RoundDto>>;