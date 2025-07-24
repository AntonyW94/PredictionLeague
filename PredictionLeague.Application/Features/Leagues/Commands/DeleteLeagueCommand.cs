using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record DeleteLeagueCommand(
    int LeagueId,
    string DeletingUserId,
    bool IsAdmin) : IRequest, ITransactionalRequest;