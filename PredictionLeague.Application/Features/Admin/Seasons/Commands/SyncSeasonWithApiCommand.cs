using MediatR;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record SyncSeasonWithApiCommand(int SeasonId) : IRequest;