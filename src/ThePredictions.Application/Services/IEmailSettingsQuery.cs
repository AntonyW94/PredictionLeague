namespace ThePredictions.Application.Services;

/// <summary>
/// Reads the stored email on/off switch, or <c>null</c> when no settings row has been created yet.
/// </summary>
/// <remarks>
/// Returns the stored value and nothing else. What an absent row means - emails on, per
/// <c>EmailSettings.DefaultEmailsEnabled</c> - is a rule, and it lives with the provider that caches the answer.
/// </remarks>
public interface IEmailSettingsQuery
{
    Task<bool?> GetEmailsEnabledAsync(CancellationToken cancellationToken);
}
