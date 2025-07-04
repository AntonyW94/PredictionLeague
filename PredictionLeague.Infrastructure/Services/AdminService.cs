using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Admin.Matches;
using PredictionLeague.Shared.Admin.Results;
using PredictionLeague.Shared.Admin.Rounds;
using PredictionLeague.Shared.Admin.Seasons;
using PredictionLeague.Shared.Admin.Teams;
using PredictionLeague.Shared.Leagues;
using System.Transactions;

namespace PredictionLeague.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IPredictionService _predictionService;
    private readonly ISeasonService _seasonService;

    public AdminService(
        ISeasonRepository seasonRepository,
        IRoundRepository roundRepository,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        ILeagueRepository leagueRepository,
        IPredictionService predictionService, ISeasonService seasonService)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _leagueRepository = leagueRepository;
        _predictionService = predictionService;
        _seasonService = seasonService;
    }

    #region Seasons

    public async Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync() => await _seasonService.GetAllAsync();

    public async Task CreateSeasonAsync(CreateSeasonRequest request)
    {
        var season = new Season { Name = request.Name, StartDate = request.StartDate, EndDate = request.EndDate, IsActive = true };
        await _seasonRepository.AddAsync(season);
    }

    public async Task UpdateSeasonAsync(int id, UpdateSeasonRequest request)
    {
        var season = await _seasonRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Season not found.");

        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.IsActive = request.IsActive;

        await _seasonRepository.UpdateAsync(season);
    }

    #endregion

    #region Rounds

    public async Task<IEnumerable<RoundDto>> GetRoundsForSeasonAsync(int seasonId)
    {
        var rounds = await _roundRepository.GetBySeasonIdAsync(seasonId);
        var roundsToReturn = new List<RoundDto>();

        foreach (var round in rounds)
        {
            var matches = await _matchRepository.GetByRoundIdAsync(round.Id);
            roundsToReturn.Add(new RoundDto
            {
                Id = round.Id,
                RoundNumber = round.RoundNumber,
                StartDate = round.StartDate,
                Deadline = round.Deadline,
                MatchCount = matches.Count()
            });
        }
        return roundsToReturn;
    }

    public async Task<RoundDetailsDto> GetRoundByIdAsync(int roundId)
    {
        var round = await _roundRepository.GetByIdAsync(roundId) ?? throw new KeyNotFoundException("Round not found.");
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
                HomeTeamName = m.HomeTeam?.Name ?? "N/A",
                HomeTeamLogoUrl = m.HomeTeam?.LogoUrl ?? "",
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.Name ?? "N/A",
                AwayTeamLogoUrl = m.AwayTeam?.LogoUrl ?? "",
                MatchDateTime = m.MatchDateTime,
                ActualHomeScore = m.ActualHomeTeamScore,
                ActualAwayScore = m.ActualAwayTeamScore
            }).ToList()
        };

        return response;
    }

    public async Task CreateRoundAsync(CreateRoundRequest request)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        
        var round = new Round { SeasonId = request.SeasonId, RoundNumber = request.RoundNumber, StartDate = request.StartDate, Deadline = request.Deadline };
        var createdRound = await _roundRepository.AddAsync(round);
      
        foreach (var match in request.Matches.Select(matchRequest => new Match { RoundId = createdRound.Id, HomeTeamId = matchRequest.HomeTeamId, AwayTeamId = matchRequest.AwayTeamId, MatchDateTime = matchRequest.MatchDateTime }))
        {
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
       
        foreach (var match in request.Matches.Select(matchRequest => new Match { RoundId = roundId, HomeTeamId = matchRequest.HomeTeamId, AwayTeamId = matchRequest.AwayTeamId, MatchDateTime = matchRequest.MatchDateTime }))
        {
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
    public async Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync()
    {
        var leagues = await _leagueRepository.GetAllAsync();
        var leagueDtos = new List<LeagueDto>();

        foreach (var league in leagues)
        {
            var season = await _seasonRepository.GetByIdAsync(league.SeasonId);
            var members = await _leagueRepository.GetMembersByLeagueIdAsync(league.Id);
            leagueDtos.Add(new LeagueDto
            {
                Id = league.Id,
                Name = league.Name,
                SeasonName = season?.Name ?? "N/A",
                MemberCount = members.Count(),
                EntryCode = league.EntryCode ?? "Public"
            });
        }
        return leagueDtos;
    }

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

    #region Matches

    public async Task UpdateMatchResultsAsync(List<UpdateResultRequest>? results)
    {
        if (results == null || !results.Any())
            return;

        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            foreach (var result in results)
            {
                var match = await _matchRepository.GetByIdAsync(result.MatchId);
                if (match == null)
                    continue;
                
                match.ActualHomeTeamScore = result.HomeScore;
                match.ActualAwayTeamScore = result.AwayScore;
                match.Status = MatchStatus.Completed;
               
                await _matchRepository.UpdateAsync(match);
            }
            scope.Complete();
        }

        var firstMatch = await _matchRepository.GetByIdAsync(results.First().MatchId);
        if (firstMatch != null)
            await _predictionService.CalculatePointsForRoundAsync(firstMatch.RoundId);
    }

    #endregion
}