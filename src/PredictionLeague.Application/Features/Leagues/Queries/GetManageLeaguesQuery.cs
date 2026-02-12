using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetManageLeaguesQuery(
    string UserId,
    bool IsAdmin) : IRequest<ManageLeaguesDto>;