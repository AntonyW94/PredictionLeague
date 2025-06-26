using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Shared.Admin;
using System.Transactions;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrator")]
    public class AdminController : ControllerBase
    {
        private readonly IGameYearRepository _gameYearRepository;
        private readonly IRoundRepository _roundRepository;
        private readonly IMatchRepository _matchRepository;

        public AdminController(
            IGameYearRepository gameYearRepository,
            IRoundRepository roundRepository,
            IMatchRepository matchRepository)
        {
            _gameYearRepository = gameYearRepository;
            _roundRepository = roundRepository;
            _matchRepository = matchRepository;
        }

        [HttpPost("gameyear")]
        public async Task<IActionResult> CreateGameYear([FromBody] CreateGameYearRequest request)
        {
            var gameYear = new GameYear
            {
                YearName = request.YearName,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true
            };
            await _gameYearRepository.AddAsync(gameYear);
            return Ok(gameYear);
        }

        [HttpPost("round")]
        public async Task<IActionResult> CreateRound([FromBody] CreateRoundRequest request)
        {
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                var round = new Round
                {
                    GameYearId = request.GameYearId,
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
    }
}