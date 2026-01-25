using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Infrastructure.Services;

public class LeagueMembershipService : ILeagueMembershipService
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public LeagueMembershipService(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<bool> IsApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [LeagueMembers]
            WHERE [LeagueId] = @LeagueId
              AND [UserId] = @UserId
              AND [Status] = @ApprovedStatus;";

        var count = await _dbConnection.QuerySingleOrDefaultAsync<int>(
            sql,
            cancellationToken,
            new { LeagueId = leagueId, UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return count > 0;
    }

    public async Task EnsureApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        var isMember = await IsApprovedMemberAsync(leagueId, userId, cancellationToken);

        if (!isMember)
            throw new UnauthorizedAccessException("You must be a member of this league to access this resource.");
    }

    public async Task<bool> IsLeagueAdministratorAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [Leagues]
            WHERE [Id] = @LeagueId
              AND [AdministratorUserId] = @UserId;";

        var count = await _dbConnection.QuerySingleOrDefaultAsync<int>(
            sql,
            cancellationToken,
            new { LeagueId = leagueId, UserId = userId });

        return count > 0;
    }

    public async Task EnsureLeagueAdministratorAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        var isAdmin = await IsLeagueAdministratorAsync(leagueId, userId, cancellationToken);

        if (!isAdmin)
            throw new UnauthorizedAccessException("Only the league administrator can access this resource.");
    }
}
