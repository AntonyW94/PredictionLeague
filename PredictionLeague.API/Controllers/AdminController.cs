using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Shared.Admin;
using PredictionLeague.Shared.Admin.Teams;
using System.Transactions;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrator")]
    public class AdminController : ControllerBase
    {
        private readonly ISeasonRepository _seasonRepository;
        private readonly IRoundRepository _roundRepository;
        private readonly IMatchRepository _matchRepository;
        private readonly ITeamRepository _teamRepository;

        public AdminController(
            ISeasonRepository seasonRepository,
            IRoundRepository roundRepository,
            IMatchRepository matchRepository, ITeamRepository teamRepository)
        {
            _seasonRepository = seasonRepository;
            _roundRepository = roundRepository;
            _matchRepository = matchRepository;
            _teamRepository = teamRepository;
        }

        [HttpPost("season")]
        public async Task<IActionResult> CreateSeason([FromBody] CreateSeasonRequest request)
        {
            var season = new Season
            {
                Name = request.Name,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true
            };
            await _seasonRepository.AddAsync(season);
            return Ok(season);
        }

        [HttpPost("round")]
        public async Task<IActionResult> CreateRound([FromBody] CreateRoundRequest request)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                var round = new Round
                {
                    SeasonId = request.SeasonId,
                    RoundNumber = request.RoundNumber,
                    StartDate = request.StartDate,
                    Deadline = request.Deadline
                };

                var createdRound = await _roundRepository.AddAsync(round);

                foreach (var matchRequest in request.Matches)
                {
                    var match = new Match
                    {
                        RoundId = createdRound.Id,
                        HomeTeamId = matchRequest.HomeTeamId,
                        AwayTeamId = matchRequest.AwayTeamId,
                        MatchDateTime = matchRequest.MatchDateTime
                    };
                    await _matchRepository.AddAsync(match);
                }

                scope.Complete();
            }

            return Ok(new { message = "Round and matches created successfully." });
        }

        #region Teams

        [HttpPost("teams")]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request)
        {
            var newTeam = new Team
            {
                Name = request.Name,
                LogoUrl = request.LogoUrl
            };

            var createdTeam = await _teamRepository.AddAsync(newTeam);

            return CreatedAtAction(nameof(TeamsController.GetTeamById), "Teams", new { id = createdTeam.Id }, createdTeam);
        }

        [HttpPut("teams/{id}")]
        public async Task<IActionResult> UpdateTeam(int id, [FromBody] UpdateTeamRequest request)
        {
            var team = await _teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound("Team not found.");
            }

            team.Name = request.Name;
            team.LogoUrl = request.LogoUrl;

            await _teamRepository.UpdateAsync(team);

            return Ok(new { message = "Team updated successfully." });
        }

        #endregion
    }
}