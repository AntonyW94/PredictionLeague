using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record RemoveRejectedLeagueCommand(
    int LeagueId,
    string CurrentUserId) : IRequest, ITransactionalRequest;