using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Repositories.PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services.PredictionLeague.Core.Services;
using PredictionLeague.Shared.Admin;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Admin.Rounds;
using PredictionLeague.Shared.Admin.Seasons;
using PredictionLeague.Shared.Admin.Teams;
using PredictionLeague.Shared.Leagues;
using System.Transactions;

namespace PredictionLeague.Infrastructure.Services
{
    public class AdminService : IAdminService
    {
        private readonly ISeasonRepository _seasonRepository;
        private readonly IRoundRepository _roundRepository;
        private readonly IMatchRepository _matchRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly ILeagueRepository _leagueRepository;

        public AdminService(
            ISeasonRepository seasonRepository,
            IRoundRepository roundRepository,
            IMatchRepository matchRepository,
            ITeamRepository teamRepository,
            ILeagueRepository leagueRepository)
        {
            _seasonRepository = seasonRepository;
            _roundRepository = roundRepository;
            _matchRepository = matchRepository;
            _teamRepository = teamRepository;
            _leagueRepository = leagueRepository;
        }

        #region Seasons
        public async Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync() => await _seasonRepository.GetAllAsync();

        public async Task CreateSeasonAsync(CreateSeasonRequest request)
        {
            var season = new Season { Name = request.Name, StartDate = request.StartDate, EndDate = request.EndDate, IsActive = true };
            await _seasonRepository.AddAsync(season);
        }

        public async Task UpdateSeasonAsync(int id, UpdateSeasonRequest request)
        {
            await _seasonRepository.UpdateAsync(id, request);
        }
        #endregion

        #region Rounds
        public async Task<IEnumerable<RoundDto>> GetRoundsForSeasonAsync(int seasonId) => await _roundRepository.GetBySeasonIdAsync(seasonId);

        public async Task<RoundDetailsDto> GetRoundByIdAsync(int roundId)
        {
            var round = await _roundRepository.GetByIdAsync(roundId) ?? throw new KeyNotFoundException("Round not found.");
            var matches = await _matchRepository.GetByRoundIdAsync(roundId);
            return new RoundDetailsDto
            {
                Round = new RoundDto { Id = round.Id, SeasonId = round.SeasonId, RoundNumber = round.RoundNumber, StartDate = round.StartDate, Deadline = round.Deadline },
                Matches = matches.Select(m => new Shared.Admin.Matches.MatchDto { Id = m.Id, HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId, MatchDateTime = m.MatchDateTime }).ToList()
            };
        }

        public async Task CreateRoundAsync(CreateRoundRequest request)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var round = new Round { SeasonId = request.SeasonId, RoundNumber = request.RoundNumber, StartDate = request.StartDate, Deadline = request.Deadline };
            var createdRound = await _roundRepository.AddAsync(round);
            foreach (var matchRequest in request.Matches)
            {
                var match = new Match { RoundId = createdRound.Id, HomeTeamId = matchRequest.HomeTeamId, AwayTeamId = matchRequest.AwayTeamId, MatchDateTime = matchRequest.MatchDateTime };
                await _matchRepository.AddAsync(match);
            }
            scope.Complete();
        }

        public async Task UpdateRoundAsync(int roundId, UpdateRoundRequest request)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var round = await _roundRepository.GetByIdAsync(roundId) ?? throw new KeyNotFoundException("Round not found.");
            round.StartDate = request.StartDate;
            round.Deadline = request.Deadline;
            await _roundRepository.UpdateAsync(round);
            await _matchRepository.DeleteByRoundIdAsync(roundId);
            foreach (var matchRequest in request.Matches)
            {
                var match = new Match { RoundId = roundId, HomeTeamId = matchRequest.HomeTeamId, AwayTeamId = matchRequest.AwayTeamId, MatchDateTime = matchRequest.MatchDateTime };
                await _matchRepository.AddAsync(match);
            }
            scope.Complete();
        }
        #endregion

        #region Teams
        public async Task<Team> CreateTeamAsync(CreateTeamRequest request)
        {
            var newTeam = new Team { Name = request.Name, LogoUrl = request.LogoUrl };
            return await _teamRepository.AddAsync(newTeam);
        }

        public async Task UpdateTeamAsync(int id, UpdateTeamRequest request)
        {
            var team = await _teamRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Team not found.");
            team.Name = request.Name;
            team.LogoUrl = request.LogoUrl;
            await _teamRepository.UpdateAsync(team);
        }
        #endregion

        #region Leagues
        public async Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync() => await _leagueRepository.GetAllAsync();

        public async Task CreateLeagueAsync(CreateLeagueRequest request, string administratorId)
        {
            var newLeague = new League { SeasonId = request.SeasonId, Name = request.Name, EntryCode = string.IsNullOrWhiteSpace(request.EntryCode) ? null : request.EntryCode, AdministratorUserId = administratorId };
            await _leagueRepository.CreateAsync(newLeague);
        }

        public async Task UpdateLeagueAsync(int id, UpdateLeagueRequest request)
        {
            var league = await _leagueRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException("League not found.");
            league.Name = request.Name;
            league.EntryCode = string.IsNullOrWhiteSpace(request.EntryCode) ? null : request.EntryCode;
            await _leagueRepository.UpdateAsync(league);
        }
        #endregion
    }
}
