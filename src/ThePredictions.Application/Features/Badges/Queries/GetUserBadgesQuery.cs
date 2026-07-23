using MediatR;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Application.Features.Badges.Queries;

public record GetUserBadgesQuery(string UserId) : IRequest<UserBadgesDto>;
