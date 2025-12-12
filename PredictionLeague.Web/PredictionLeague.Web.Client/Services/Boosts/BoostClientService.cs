using PredictionLeague.Contracts.Boosts;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Services.Boosts;

public class BoostClientService
{
    private readonly HttpClient _http;

    public BoostClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<BoostOptionDto>?> GetAvailableBoostsAsync(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        var url = $"api/boosts/available?leagueId={leagueId}&roundId={roundId}";
        try
        {
            return await _http.GetFromJsonAsync< List<BoostOptionDto>>(url, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }
   
    public async Task<ApplyBoostResultDto?> ApplyBoostAsync(int leagueId, int roundId, string boostCode, CancellationToken cancellationToken)
    {
        var request = new { LeagueId = leagueId, RoundId = roundId, BoostCode = boostCode };
        try
        {
            var result = await _http.PostAsJsonAsync("api/boosts/apply", request, cancellationToken);
            if (result.IsSuccessStatusCode)
                return await result.Content.ReadFromJsonAsync<ApplyBoostResultDto>(cancellationToken);
                
            var body = await result.Content.ReadFromJsonAsync<ApplyBoostResultDto>(cancellationToken);
            return body ?? new ApplyBoostResultDto { Success = false, Error = $"Server returned {result.StatusCode}" };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new ApplyBoostResultDto { Success = false, Error = ex.Message };
        }
    }

    public async Task<bool> DeleteUserBoostUsageAsync(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        var response = await _http.DeleteAsync($"api/boosts/user/usage?leagueId={leagueId}&roundId={roundId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}