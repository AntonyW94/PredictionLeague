using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailSettings;

/// <summary>
/// Update for the global master email switch. When <see cref="EmailsEnabled"/> is false the app suppresses all
/// automated, transactional emails.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateEmailSettingsRequest
{
    public bool EmailsEnabled { get; set; }
}
