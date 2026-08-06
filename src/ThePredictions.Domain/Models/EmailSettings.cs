using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Models;

/// <summary>
/// Global, admin-editable master switch for the app's automated, transactional emails (round digests,
/// reminders, welcome and prize emails). Stored as a single row so it can be flipped without a code deploy -
/// primarily so the dev environment can be silenced when no one is testing. Defaults to enabled, so a fresh
/// or unseeded database (such as production) keeps sending emails.
/// </summary>
public class EmailSettings
{
    public const bool DefaultEmailsEnabled = true;

    public int Id { get; init; }
    public bool EmailsEnabled { get; private set; }

    [ExcludeFromCodeCoverage(Justification = "Parameterless constructor for Dapper hydration: no logic to test.")]
    private EmailSettings() { }

    public EmailSettings(int id, bool emailsEnabled)
    {
        Id = id;
        EmailsEnabled = emailsEnabled;
    }

    /// <summary>The built-in default, used to seed the row and as a fallback when none is stored yet.</summary>
    public static EmailSettings CreateDefault() => new()
    {
        EmailsEnabled = DefaultEmailsEnabled
    };

    public void Update(bool emailsEnabled)
    {
        EmailsEnabled = emailsEnabled;
    }
}
