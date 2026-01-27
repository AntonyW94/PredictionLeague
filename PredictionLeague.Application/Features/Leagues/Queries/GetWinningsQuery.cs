using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetWinningsQuery(int LeagueId, string CurrentUserId) : IRequest<WinningsDto>;