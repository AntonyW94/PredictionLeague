using MediatR;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record UpdateScoresForNextRoundCommand(int SeasonId) : IRequest;