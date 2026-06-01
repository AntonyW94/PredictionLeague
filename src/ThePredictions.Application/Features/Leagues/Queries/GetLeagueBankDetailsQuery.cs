using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetLeagueBankDetailsQuery(int LeagueId, string RequestingUserId) : IRequest<LeagueBankDetailsDto>;
