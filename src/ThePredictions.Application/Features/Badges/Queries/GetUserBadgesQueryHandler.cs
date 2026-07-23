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

        const string nameSql = "SELECT [FirstName] + ' ' + LEFT([LastName], 1) FROM [AspNetUsers] WHERE [Id] = @UserId;";
        var ownerName = await dbConnection.QuerySingleOrDefaultAsync<string>(nameSql, cancellationToken, new { request.UserId }) ?? string.Empty;

        return BadgeCatalogue.BuildPage(state, dateTimeProvider.UtcNow) with { OwnerName = ownerName };
    }
}
