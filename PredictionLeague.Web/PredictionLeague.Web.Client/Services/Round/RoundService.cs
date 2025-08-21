namespace PredictionLeague.Web.Client.Services.Round;

public class RoundService : IRoundService
{
    private readonly HttpClient _httpClient;

    public RoundService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> SendChaseEmailsAsync(int roundId)
    {
        var response = await _httpClient.PostAsync($"api/admin/rounds/{roundId}/send-prediction-reminder-emails", null);
        return response.IsSuccessStatusCode;
    }
}