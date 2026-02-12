using MediatR;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record UpdateSeasonStatusCommand(int SeasonId, bool IsActive) : IRequest;