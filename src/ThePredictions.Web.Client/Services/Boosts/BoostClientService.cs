using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Boosts;
using System.Net.Http.Json;

namespace ThePredictions.Web.Client.Services.Boosts;

[ExcludeFromCodeCoverage(Justification = "Typed HttpClient wrapper: forwards to an API endpoint and deserialises the reply.")]
public class BoostClientService(HttpClient http)
{
    public async Task<List<BoostOptionDto>?> GetAvailableBoostsAsync(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        var url = $"api/boosts/available?leagueId={leagueId}&roundId={roundId}";
        try
        {
            return await http.GetFromJsonAsync< List<BoostOptionDto>>(url, cancellationToken);
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
            var result = await http.PostAsJsonAsync("api/boosts/apply", request, cancellationToken);
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
        var response = await http.DeleteAsync($"api/boosts/user/usage?leagueId={leagueId}&roundId={roundId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
