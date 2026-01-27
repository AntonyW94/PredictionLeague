using Microsoft.AspNetCore.Components;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.ViewModels.Admin.Rounds;

public class EnterResultsViewModel(HttpClient http, NavigationManager navigationManager)
{
    public List<MatchViewModel> Matches { get; private set; } = [];
    public int RoundNumber { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    private int _seasonId;

    public async Task LoadRoundDetails(int roundId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var roundDetails = await http.GetFromJsonAsync<RoundDetailsDto>($"api/admin/rounds/{roundId}");
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

        var response = await http.PutAsJsonAsync($"api/admin/rounds/{roundId}/results", resultsToUpdate);
        if (response.IsSuccessStatusCode)
        {
            SuccessMessage = "Results saved and points calculated successfully!";
            await Task.Delay(1500);
            navigationManager.NavigateTo("/dashboard", forceLoad: true);
        }
        else
        {
            ErrorMessage = "There was an error saving the results.";
        }

        IsBusy = false;
    }

    public void BackToRounds()
    {
        navigationManager.NavigateTo($"/admin/seasons/{_seasonId}/rounds");
    }
}