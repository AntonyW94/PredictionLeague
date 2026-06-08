using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyMemberOfLeagueApprovalCommandHandler(IApplicationReadDbConnection dbConnection, IEmailService emailService, IOptions<BrevoSettings> brevoSettings) : IRequestHandler<NotifyMemberOfLeagueApprovalCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;

    public async Task Handle(NotifyMemberOfLeagueApprovalCommand request, CancellationToken cancellationToken)
    {
        if (_brevoSettings.Templates == null)
            return;

        var templateId = _brevoSettings.Templates.LeagueJoinApproved;

        // 0 = the "you can now take part" template has not been configured in Brevo yet; skip sending
        // rather than calling the API with an invalid template id.
        if (templateId == 0)
            return;

        // Read only the member (AspNetUsers) and the season (Seasons) - the league name is supplied by
        // the caller, which already holds the aggregate. Avoids touching [Leagues], whose row an in-flight
        // join transaction may have locked.
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
                    u.[Id] = @MemberUserId
                    AND s.[Id] = @SeasonId;";

        var member = await dbConnection.QuerySingleOrDefaultAsync<LeagueMemberContactDto>(sql, cancellationToken, new { request.MemberUserId, request.SeasonId });
        if (member != null)
        {
            var parameters = new
            {
                FIRST_NAME = member.FirstName,
                LEAGUE_NAME = request.LeagueName,
                SEASON_NAME = member.SeasonName
            };

            await emailService.SendTemplatedEmailAsync(member.Email, templateId, parameters);
        }
    }
}

public record LeagueMemberContactDto(string Email, string FirstName, string SeasonName);
