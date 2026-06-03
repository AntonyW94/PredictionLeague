using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyLeagueAdminOfJoinRequestCommandHandler(IApplicationReadDbConnection dbConnection, IEmailService emailService, IOptions<BrevoSettings> brevoSettings) : IRequestHandler<NotifyLeagueAdminOfJoinRequestCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;

    public async Task Handle(NotifyLeagueAdminOfJoinRequestCommand request, CancellationToken cancellationToken)
    {
        if (_brevoSettings.Templates == null)
            return;
        
        // Read only the admin (AspNetUsers) and the season (Seasons) - the league name is supplied by
        // the caller, which already holds the aggregate. This deliberately avoids touching [Leagues],
        // whose row the in-flight join transaction has locked; reading it here on a separate connection
        // would block until that transaction commits (the request never gets that far) and time out.
        const string sql = @"
                SELECT
                    u.[Email],
                    u.[FirstName],
                    s.[Name] AS SeasonName
                FROM
                    [AspNetUsers] u
                CROSS JOIN
                    [Seasons] s
                WHERE
                    u.[Id] = @AdministratorUserId
                    AND s.[Id] = @SeasonId;";

        var admin = await dbConnection.QuerySingleOrDefaultAsync<LeagueAdminDto>(sql, cancellationToken, new { request.AdministratorUserId, request.SeasonId });
        if (admin != null)
        {
            var templateId = _brevoSettings.Templates.JoinLeagueRequest;

            var parameters = new
            {
                FIRST_NAME = request.NewMemberFirstName,
                LAST_NAME = request.NewMemberLastName,
                LEAGUE_NAME = request.LeagueName,
                SEASON_NAME = admin.SeasonName,
                ADMIN_NAME = admin.FirstName
            };

            await emailService.SendTemplatedEmailAsync(admin.Email, templateId, parameters);
        }
    }
}

public record LeagueAdminDto(string Email, string FirstName, string SeasonName);