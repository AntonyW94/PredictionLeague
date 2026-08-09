using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyLeagueAdminOfJoinRequestCommandHandler(IApplicationReadDbConnection dbConnection, IEmailService emailService, IOptions<BrevoSettings> brevoSettings, IOptions<SiteSettings> siteSettings) : IRequestHandler<NotifyLeagueAdminOfJoinRequestCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

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

        var admin = await dbConnection.QuerySingleOrDefaultAsync<LeagueAdminRow>(sql, cancellationToken, new { request.AdministratorUserId, request.SeasonId });
        if (admin != null)
        {
            var templateId = _brevoSettings.Templates.JoinLeagueRequest;

            var parameters = new
            {
                FIRST_NAME = request.NewMemberFirstName,
                LAST_NAME = request.NewMemberLastName,
                LEAGUE_NAME = request.LeagueName,
                SEASON_NAME = admin.SeasonName,
                ADMIN_NAME = admin.FirstName,
                // Deep-links to the dashboard's Admin tab, where pending join requests are actioned.
                // Built from configured site settings, never a request header (attacker-controllable).
                DASHBOARD_URL = $"{_siteSettings.ResolvedBaseUrl}/dashboard?tab=admin"
            };

            await emailService.SendTemplatedEmailAsync(admin.Email, templateId, parameters);
        }
    }

    // internal, not public: the Dto suffix is reserved for ThePredictions.Contracts, and keeping the
    // row type inside the handler confines the positional SELECT-to-record coupling to this one file.
    // InternalsVisibleTo already exposes this assembly to ThePredictions.Application.Tests.Unit.
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal record LeagueAdminRow(string Email, string FirstName, string SeasonName);
}