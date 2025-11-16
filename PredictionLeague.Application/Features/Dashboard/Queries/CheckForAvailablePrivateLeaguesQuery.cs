using MediatR;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record CheckForAvailablePrivateLeaguesQuery(string UserId) : IRequest<bool>;