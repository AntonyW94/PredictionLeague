using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Badges.Queries;

public class GetUserBadgesQueryHandler(IApplicationReadDbConnection dbConnection, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetUserBadgesQuery, UserBadgesDto>
{
    public async Task<UserBadgesDto> Handle(GetUserBadgesQuery request, CancellationToken cancellationToken)
    {
        var state = await BadgeStateQueries.LoadAsync(dbConnection, request.UserId, cancellationToken);
        return BadgeCatalogue.BuildPage(state, dateTimeProvider.UtcNow);
    }
}
