using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record CancelLeagueRequestCommand(int LeagueId, string UserId) : IRequest;