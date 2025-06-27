using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Shared.Admin;
using PredictionLeague.Shared.Admin.Matches;
using PredictionLeague.Shared.Admin.Rounds;
using PredictionLeague.Shared.Admin.Seasons;
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

        #region Seasons 

        [HttpGet("seasons")]
        public async Task<IActionResult> GetAllSeasons()
        {
            var seasons = await _seasonRepository.GetAllAsync();
            return Ok(seasons);
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

        [HttpPut("seasons/{id}")]
        public async Task<IActionResult> UpdateSeason(int id, [FromBody] UpdateSeasonRequest request)
        {
            var seasonDto = await _seasonRepository.GetByIdAsync(id);
            if (seasonDto == null)
                return NotFound("Season not found.");

            seasonDto.Name = request.Name;
            seasonDto.StartDate = request.StartDate;
            seasonDto.EndDate = request.EndDate;
            seasonDto.IsActive = request.IsActive;

            var season = new Season
            {
                Name = seasonDto.Name,
                StartDate = seasonDto.StartDate,
                EndDate = seasonDto.EndDate,
                IsActive = seasonDto.IsActive
            };

            await _seasonRepository.UpdateAsync(season);
            return Ok(new { message = "Season updated successfully." });
        }


        [HttpGet("seasons/{seasonId}/rounds")]
        public async Task<IActionResult> GetRoundsForSeason(int seasonId)
        {
            var rounds = await _roundRepository.GetBySeasonIdAsync(seasonId);
            return Ok(rounds);
        }

        #endregion

        #region Rounds

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

        [HttpGet("rounds/{roundId}")]
        public async Task<IActionResult> GetRoundById(int roundId)
        {
            var round = await _roundRepository.GetByIdAsync(roundId);
            if (round == null) return NotFound();

            var matches = await _matchRepository.GetByRoundIdAsync(roundId);

            var response = new RoundDetailsDto
            {
                Round = new RoundDto
                {
                    Id = round.Id,
                    SeasonId = round.SeasonId,
                    RoundNumber = round.RoundNumber,
                    StartDate = round.StartDate,
                    Deadline = round.Deadline
                },
                Matches = matches.Select(m => new MatchDto
                {
                    Id = m.Id,
                    HomeTeamId = m.HomeTeamId,
                    AwayTeamId = m.AwayTeamId,
                    MatchDateTime = m.MatchDateTime
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPut("rounds/{roundId}")]
        public async Task<IActionResult> UpdateRound(int roundId, [FromBody] UpdateRoundRequest request)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                var round = await _roundRepository.GetByIdAsync(roundId);
                if (round == null)
                    return NotFound("Round not found.");

                round.StartDate = request.StartDate;
                round.Deadline = request.Deadline;
               
                await _roundRepository.UpdateAsync(round);

                // A simple way to handle match updates: delete all existing matches and re-add them.
                await _matchRepository.DeleteByRoundIdAsync(roundId); // Assumes this method exists
                foreach (var matchRequest in request.Matches)
                {
                    var match = new Match
                    {
                        RoundId = roundId,
                        HomeTeamId = matchRequest.HomeTeamId,
                        AwayTeamId = matchRequest.AwayTeamId,
                        MatchDateTime = matchRequest.MatchDateTime
                    };
                    await _matchRepository.AddAsync(match);
                }

                scope.Complete();
            }
            return Ok(new { message = "Round updated successfully." });
        }

        #endregion

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