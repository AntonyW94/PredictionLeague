using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Application.Features.Badges.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetBadgeLeaderboardQuery(string UserId) : IRequest<BadgeLeaderboardDto>;
