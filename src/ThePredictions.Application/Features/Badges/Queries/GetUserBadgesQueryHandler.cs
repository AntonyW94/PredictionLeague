using MediatR;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>The badges page: what this player holds, and how close they are to the rest.</summary>
public class GetUserBadgesQueryHandler(IBadgeStateQuery badgeStateQuery, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetUserBadgesQuery, UserBadgesDto>
{
    public async Task<UserBadgesDto> Handle(GetUserBadgesQuery request, CancellationToken cancellationToken)
    {
        var data = await badgeStateQuery.ExecuteAsync(request.UserId, cancellationToken);

        // The page can be looked at for someone else, so it is titled with whose badges these are - shown the way
        // players are shown to each other everywhere, as a first name and a last initial.
        var ownerName = PlayerDisplayName.Format(data.OwnerFirstName, data.OwnerLastName);

        return BadgeCatalogue.BuildPage(BadgeState.From(data), dateTimeProvider.UtcNow) with { OwnerName = ownerName };
    }
}
