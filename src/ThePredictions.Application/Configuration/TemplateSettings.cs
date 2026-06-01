using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TemplateSettings
{
    public long JoinLeagueRequest { get; set; }
    public long PredictionsMissing { get; set; }
    public long PasswordReset { get; set; }
    public long PasswordResetGoogleUser { get; set; }
    public long EmailConfirmation { get; set; }
}