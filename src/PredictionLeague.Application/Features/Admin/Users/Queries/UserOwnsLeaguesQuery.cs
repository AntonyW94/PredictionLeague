using MediatR;

namespace PredictionLeague.Application.Features.Admin.Users.Queries;

public record UserOwnsLeaguesQuery(string UserId) : IRequest<bool>;