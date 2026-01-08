using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record DismissRejectedNotificationCommand(int LeagueId, string UserId) : IRequest;