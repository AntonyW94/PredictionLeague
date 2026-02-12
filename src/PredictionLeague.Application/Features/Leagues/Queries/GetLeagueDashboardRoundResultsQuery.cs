using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetLeagueDashboardRoundResultsQuery(
    int LeagueId,
    int RoundId,
    string CurrentUserId) : IRequest<IEnumerable<PredictionResultDto>?>;