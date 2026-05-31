using MediatR;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public record AcquireSeasonPassCommand(string UserId, int SeasonId) : IRequest;
