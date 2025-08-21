using MediatR;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record SendPredictionsMissingEmailsCommand(int RoundId) : IRequest;