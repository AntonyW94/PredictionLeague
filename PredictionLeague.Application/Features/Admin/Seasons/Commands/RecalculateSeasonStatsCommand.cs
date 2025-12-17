using MediatR;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record RecalculateSeasonStatsCommand(int SeasonId) : IRequest;