namespace ThePredictions.Application.Services;

/// <summary>
/// Cheap, cached access to the database-backed email master switch, for use on the hot path inside the email
/// service (which can send many emails in a loop). Reads are cached briefly so a single digest run does not hit
/// the database once per recipient.
/// </summary>
public interface IEmailSettingsProvider
{
    Task<bool> AreEmailsEnabledAsync(CancellationToken cancellationToken);
}
