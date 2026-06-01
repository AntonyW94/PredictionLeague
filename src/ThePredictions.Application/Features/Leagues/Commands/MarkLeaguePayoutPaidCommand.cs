using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record MarkLeaguePayoutPaidCommand(int LeagueId, string WinnerUserId, string RequestingUserId) : IRequest;
