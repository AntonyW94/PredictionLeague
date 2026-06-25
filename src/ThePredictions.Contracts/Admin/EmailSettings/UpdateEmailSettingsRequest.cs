namespace ThePredictions.Contracts.Admin.EmailSettings;

/// <summary>
/// Update for the global master email switch. When <see cref="EmailsEnabled"/> is false the app suppresses all
/// automated, transactional emails.
/// </summary>
public class UpdateEmailSettingsRequest
{
    public bool EmailsEnabled { get; set; }
}
