using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetLeaguePrizesPageQuery(int LeagueId) : IRequest<LeaguePrizesPageDto?>;