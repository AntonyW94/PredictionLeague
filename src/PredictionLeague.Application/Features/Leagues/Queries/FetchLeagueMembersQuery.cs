using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record FetchLeagueMembersQuery(int LeagueId, string CurrentUserId) : IRequest<LeagueMembersPageDto?>;