using MediatR;
using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands
{
    public class SendPredictionsMissingEmailsCommandHandler : IRequestHandler<SendPredictionsMissingEmailsCommand>
    {
        private readonly IApplicationReadDbConnection _dbConnection;
        private readonly IEmailService _emailService;
        private readonly BrevoSettings _brevoSettings;

        public SendPredictionsMissingEmailsCommandHandler(
            IApplicationReadDbConnection dbConnection,
            IEmailService emailService,
            IOptions<BrevoSettings> brevoSettings)
        {
            _dbConnection = dbConnection;
            _emailService = emailService;
            _brevoSettings = brevoSettings.Value;
        }

        public async Task Handle(SendPredictionsMissingEmailsCommand request, CancellationToken cancellationToken)
        {
            const string sql = @"
            SELECT DISTINCT
                u.[Email],
                u.[FirstName],
                'Round ' + CONVERT(NVARCHAR(MAX), r.[RoundNumber]) AS RoundName,
                r.[Deadline]
            FROM 
                [AspNetUsers] u
            JOIN 
                [LeagueMembers] lm ON u.[Id] = lm.[UserId]
            JOIN 
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN 
                [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            WHERE 
                r.[Id] = @RoundId
              AND lm.[Status] = @ApprovedStatus
              AND NOT EXISTS (
                  SELECT 1 FROM [UserPredictions] up
                  JOIN [Matches] m ON up.[MatchId] = m.[Id]
                  WHERE m.[RoundId] = r.[Id] AND up.[UserId] = u.[Id]
              );";

            var usersToChase = await _dbConnection.QueryAsync<ChaseUserDto>(sql, cancellationToken, new { request.RoundId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
            var templateId = _brevoSettings.Templates.PredictionsMissing;

            foreach (var user in usersToChase)
            {
                var parameters = new
                {
                    FIRST_NAME = user.FirstName,
                    ROUND_NAME = user.RoundName,
                    DEADLINE = user.Deadline.ToString("dddd, dd MMMM yyyy 'at' HH:mm")
                };
                await _emailService.SendTemplatedEmailAsync(user.Email, templateId, parameters);
            }
        }
    }

    public record ChaseUserDto(string Email, string FirstName, string RoundName, DateTime Deadline);
}
