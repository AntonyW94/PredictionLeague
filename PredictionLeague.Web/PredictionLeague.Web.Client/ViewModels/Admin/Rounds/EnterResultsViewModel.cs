using Microsoft.AspNetCore.Components;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.ViewModels.Admin.Rounds;

public class EnterResultsViewModel
{
    public List<MatchViewModel> Matches { get; private set; } = new();
    public int RoundNumber { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    private int _seasonId;
    private readonly HttpClient _http;
    private readonly NavigationManager _navigationManager;

    public EnterResultsViewModel(HttpClient http, NavigationManager navigationManager)
    {
        _http = http;
        _navigationManager = navigationManager;
    }

    public async Task LoadRoundDetails(int roundId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var roundDetails = await _http.GetFromJsonAsync<RoundDetailsDto>($"api/rounds/{roundId}");
            if (roundDetails != null)
            {
                _seasonId = roundDetails.Round.SeasonId;
                RoundNumber = roundDetails.Round.RoundNumber;
                Matches = roundDetails.Matches.Select(m => new MatchViewModel(m)).ToList();
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load round details.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task HandleSaveResultsAsync(int roundId)
    {
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;

        var resultsToUpdate = Matches.Select(m => new MatchResultDto(
            m.MatchId,
            m.HomeScore,
            m.AwayScore,
            m.Status
        )).ToList();

        var response = await _http.PutAsJsonAsync($"api/rounds/{roundId}/results", resultsToUpdate);

        if (response.IsSuccessStatusCode)
        {
            SuccessMessage = "Results saved and points calculated successfully!";
        }
        else
        {
            ErrorMessage = "There was an error saving the results.";
        }

        IsBusy = false;
    }

    public void BackToRounds()
    {
        _navigationManager.NavigateTo($"/admin/seasons/{_seasonId}/rounds");
    }
}

public class MatchViewModel
{
    public int MatchId { get; set; }
    public DateTime MatchDateTime { get; set; }
    public string HomeTeamName { get; set; }
    public string? HomeTeamLogoUrl { get; set; }
    public string AwayTeamName { get; set; }
    public string? AwayTeamLogoUrl { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public MatchStatus Status { get; set; }

    // This constructor now uses the correct DTO
    public MatchViewModel(MatchInRoundDto match)
    {
        MatchId = match.Id;
        MatchDateTime = match.MatchDateTime;
        HomeTeamName = match.HomeTeamName;
        HomeTeamLogoUrl = match.HomeTeamLogoUrl;
        AwayTeamName = match.AwayTeamName;
        AwayTeamLogoUrl = match.AwayTeamLogoUrl;
        HomeScore = match.ActualHomeScore ?? 0;
        AwayScore = match.ActualAwayScore ?? 0;
        Status = match.Status;
    }

    public void UpdateScore(bool isHomeTeam, int delta)
    {
        if (isHomeTeam)
        {
            var newScore = HomeScore + delta;
            if (newScore >= 0 && newScore <= 20)
                HomeScore = newScore;
        }
        else
        {
            var newScore = AwayScore + delta;
            if (newScore >= 0 && newScore <= 20)
                AwayScore = newScore;
        }
    }
}