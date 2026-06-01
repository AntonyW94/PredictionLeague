using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetLeaguePaymentInfoQuery(int LeagueId, string RequestingUserId) : IRequest<LeaguePaymentInfoDto>;
