using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record JoinLeagueCommand(
    string JoiningUserId,
    string JoiningUserFirstName,
    string JoiningUserLastName,
    int? LeagueId,
    string? EntryCode,
    string? LeagueUrlBase = null
) : IRequest<int>, ITransactionalRequest;