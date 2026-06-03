using MediatR;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>The live projected prize breakdown at the current entrant count, for members and the admin.</summary>
public record GetLeaguePrizeBreakdownQuery(int LeagueId, string CurrentUserId) : IRequest<PrizeBreakdownDto>;
