namespace PredictionLeague.Application.Configuration;

public class FootballApiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string SchedulerApiKey { get; init; } = string.Empty;
}