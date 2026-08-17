using MediatR;
using ThePredictions.Contracts.Admin.Users;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

public record GetUserDeletionImpactQuery(string UserId) : IRequest<UserDeletionImpactDto>;
