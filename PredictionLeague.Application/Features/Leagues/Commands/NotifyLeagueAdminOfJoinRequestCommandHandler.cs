using MediatR;
using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;

namespace PredictionLeague.Application.Features.Leagues.Commands
{
    public class NotifyLeagueAdminOfJoinRequestCommandHandler : IRequestHandler<NotifyLeagueAdminOfJoinRequestCommand>
    {
        private readonly IApplicationReadDbConnection _dbConnection;
        private readonly IEmailService _emailService;
        private readonly BrevoSettings _brevoSettings;

        public NotifyLeagueAdminOfJoinRequestCommandHandler(IApplicationReadDbConnection dbConnection, IEmailService emailService, IOptions<BrevoSettings> brevoSettings)
        {
            _dbConnection = dbConnection;
            _emailService = emailService;
            _brevoSettings = brevoSettings.Value;
        }

        public async Task Handle(NotifyLeagueAdminOfJoinRequestCommand request, CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT 
                    u.[Email],
                    u.[FirstName], 
                    l.[Name] AS LeagueName,
                    s.[Name] AS SeasonName
                FROM 
                    [AspNetUsers] u 
                JOIN 
                    [Leagues] l ON u.[Id] = l.[AdministratorUserId]
                JOIN 
                    [Seasons] s ON l.[SeasonId] = s.[Id]
                WHERE 
                    l.[Id] = @LeagueId;";

            var admin = await _dbConnection.QuerySingleOrDefaultAsync<LeagueAdminDto>(sql, cancellationToken, new { request.LeagueId });
            if (admin != null)
            {
                var templateId = _brevoSettings.Templates.JoinLeagueRequest;

                var parameters = new
                {
                    FIRST_NAME = request.NewMemberFirstName,
                    LAST_NAME = request.NewMemberLastName,
                    LEAGUE_NAME = admin.LeagueName,
                    SEASON_NAME = admin.SeasonName,
                    ADMIN_NAME = admin.FirstName
                };

                await _emailService.SendTemplatedEmailAsync(admin.Email, templateId, parameters);
            }
        }
    }

    public record LeagueAdminDto(string Email, string FirstName, string LeagueName, string SeasonName);
}
