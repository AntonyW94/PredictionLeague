using MediatR;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Application.Features.Badges.Queries;

public record GetBadgeLeaderboardQuery(string UserId) : IRequest<BadgeLeaderboardDto>;
