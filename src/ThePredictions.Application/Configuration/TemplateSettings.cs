using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TemplateSettings
{
    public long JoinLeagueRequest { get; set; }
    public long LeagueJoinApproved { get; set; }
    public long PredictionsMissing { get; set; }
    public long PasswordReset { get; set; }
    public long PasswordResetGoogleUser { get; set; }
    public long EmailConfirmation { get; set; }
    public long RoundResultsDigest { get; set; }
    public long PrizeWon { get; set; }
}