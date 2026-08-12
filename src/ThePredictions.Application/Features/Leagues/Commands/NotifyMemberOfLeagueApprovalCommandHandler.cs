using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyMemberOfLeagueApprovalCommandHandler(ILeagueEmailRecipientQuery emailRecipientQuery, IEmailService emailService, IOptions<BrevoSettings> brevoSettings, IOptions<SiteSettings> siteSettings) : IRequestHandler<NotifyMemberOfLeagueApprovalCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;
    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task Handle(NotifyMemberOfLeagueApprovalCommand request, CancellationToken cancellationToken)
    {
        if (_brevoSettings.Templates == null)
            return;

        var templateId = _brevoSettings.Templates.LeagueJoinApproved;

        // 0 = the "you can now take part" template has not been configured in Brevo yet; skip sending
        // rather than calling the API with an invalid template id.
        if (templateId == 0)
            return;

        // Only the player and the season are read - the league's name comes from the caller, which already holds it. That
        // deliberately avoids touching [Leagues], whose row an in-flight join transaction may have locked.
        var member = await emailRecipientQuery.ExecuteAsync(request.MemberUserId, request.SeasonId, cancellationToken);
        if (member != null)
        {
            var parameters = new
            {
                FIRST_NAME = member.FirstName,
                LEAGUE_NAME = request.LeagueName,
                SEASON_NAME = member.SeasonName,
                // Built from configured site settings, never a request header (attacker-controllable).
                LEAGUE_URL = $"{_siteSettings.ResolvedBaseUrl}/leagues/{request.LeagueId}/dashboard"
            };

            await emailService.SendTemplatedEmailAsync(member.Email, templateId, parameters);
        }
    }

}
