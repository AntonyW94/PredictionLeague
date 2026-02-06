using MediatR;
using PredictionLeague.Contracts.Boosts;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetLeagueBoostUsageSummaryQuery(
    int LeagueId,
    string CurrentUserId) : IRequest<List<BoostUsageSummaryDto>>;
