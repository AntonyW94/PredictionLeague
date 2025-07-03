using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Shared.Dashboard;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IRoundRepository _roundRepository;
        private readonly IMatchRepository _matchRepository;
        private readonly IUserPredictionRepository _predictionRepository;
        private readonly ILeagueService _leagueService; // << ADDED
        private readonly ISeasonRepository _seasonRepository;

        public DashboardController(
            IRoundRepository roundRepository,
            IMatchRepository matchRepository,
            IUserPredictionRepository predictionRepository,
            ILeagueService leagueService, // << ADDED
            ISeasonRepository seasonRepository)
        {
            _roundRepository = roundRepository;
            _matchRepository = matchRepository;
            _predictionRepository = predictionRepository;
            _leagueService = leagueService; // << ADDED
            _seasonRepository = seasonRepository;
        }

        [HttpGet("dashboard-data")]
        public async Task<IActionResult> GetDashboardData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userLeagues = await _leagueService.GetLeaguesForUserAsync(userId!);

            var seasonIds = userLeagues.Select(l => l.SeasonId).Distinct();
            var upcomingRounds = new List<UpcomingRoundDto>();

            foreach (var seasonId in seasonIds)
            {
                var currentRound = await _roundRepository.GetCurrentRoundAsync(seasonId);
                if (currentRound != null)
                {
                    var season = await _seasonRepository.GetByIdAsync(seasonId);
                    var matches = await _matchRepository.GetByRoundIdAsync(currentRound.Id);
                    var userPredictions = await _predictionRepository.GetByUserIdAndRoundIdAsync(userId!, currentRound.Id);

                    upcomingRounds.Add(new UpcomingRoundDto
                    {
                        Id = currentRound.Id,
                        SeasonName = season?.Name ?? "Unknown Season",
                        RoundNumber = currentRound.RoundNumber,
                        Deadline = currentRound.Deadline,
                        Matches = matches.Select(m =>
                        {
                            var prediction = userPredictions.FirstOrDefault(p => p.MatchId == m.Id);
                            return new MatchPredictionDto
                            {
                                MatchId = m.Id,
                                MatchDateTime = m.MatchDateTime,
                                HomeTeamName = m.HomeTeam!.Name,
                                HomeTeamLogoUrl = m.HomeTeam.LogoUrl!,
                                AwayTeamName = m.AwayTeam!.Name,
                                AwayTeamLogoUrl = m.AwayTeam.LogoUrl!,
                                PredictedHomeScore = prediction?.PredictedHomeScore,
                                PredictedAwayScore = prediction?.PredictedAwayScore
                            };
                        }).ToList()
                    });
                }
            }

            var joinableLeagues = await _leagueService.GetJoinablePublicLeaguesForUserAsync(userId!);
            
            var dashboardDto = new DashboardDto
            {
                UpcomingRounds = upcomingRounds,
                JoinablePublicLeagues = joinableLeagues.ToList()
            };

            return Ok(dashboardDto);
        }
    }
}
