using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record JoinLeagueCommand(
    string JoiningUserId,
    int? LeagueId,
    string? EntryCode
) : IRequest, ITransactionalRequest;