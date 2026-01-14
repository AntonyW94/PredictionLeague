using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record JoinLeagueCommand(
    string JoiningUserId,
    string JoiningUserFirstName,
    string JoiningUserLastName,
    int? LeagueId,
    string? EntryCode
) : IRequest, ITransactionalRequest;