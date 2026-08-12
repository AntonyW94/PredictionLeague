using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyLeagueAdminOfJoinRequestCommandHandler(ILeagueEmailRecipientQuery emailRecipientQuery, IEmailService emailService, IOptions<BrevoSettings> brevoSettings, IOptions<SiteSettings> siteSettings) : IRequestHandler<NotifyLeagueAdminOfJoinRequestCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task Handle(NotifyLeagueAdminOfJoinRequestCommand request, CancellationToken cancellationToken)
    {
        if (_brevoSettings.Templates == null)
            return;
        
        // Only the administrator and the season are read - the league's name comes from the caller, which already holds it.
        // That deliberately avoids touching [Leagues], whose row the in-flight join transaction has locked; reading it here on
        // a separate connection would block until that transaction commits and then time out.
        var admin = await emailRecipientQuery.ExecuteAsync(request.AdministratorUserId, request.SeasonId, cancellationToken);
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

}