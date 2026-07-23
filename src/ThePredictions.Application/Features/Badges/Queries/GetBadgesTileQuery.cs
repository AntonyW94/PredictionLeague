using MediatR;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Application.Features.Badges.Queries;

public record GetBadgesTileQuery(string UserId) : IRequest<BadgesTileDto>;
