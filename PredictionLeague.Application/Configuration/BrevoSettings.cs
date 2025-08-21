namespace PredictionLeague.Application.Configuration;

public class BrevoSettings
{
    public string ApiKey { get; set; }
    public string SendFromName { get; set; }
    public string SendFromEmail { get; set; }
    public TemplateSettings Templates { get; set; }
}