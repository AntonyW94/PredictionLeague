using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetLeagueDashboardQuery(int LeagueId,
    string UserId,
    bool IsAdmin) : IRequest<LeagueDashboardDto?>;