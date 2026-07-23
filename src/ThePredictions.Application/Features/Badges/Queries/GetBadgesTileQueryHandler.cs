using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Badges.Queries;

public class GetBadgesTileQueryHandler(IApplicationReadDbConnection dbConnection, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetBadgesTileQuery, BadgesTileDto>
{
    public async Task<BadgesTileDto> Handle(GetBadgesTileQuery request, CancellationToken cancellationToken)
    {
        var state = await BadgeStateQueries.LoadAsync(dbConnection, request.UserId, cancellationToken);
        return BadgeCatalogue.BuildTile(state, dateTimeProvider.UtcNow);
    }
}
