using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record SetLeagueArchivedCommand(int LeagueId, string UserId, bool IsArchived) : IRequest;
