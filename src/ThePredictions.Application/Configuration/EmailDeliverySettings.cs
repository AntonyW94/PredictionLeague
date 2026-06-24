using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

/// <summary>
/// Environment-specific gating for outbound email. When <see cref="AllowedRecipients"/> is non-empty, the app
/// only sends to those addresses and silently drops everything else. This is used on the dev environment - where
/// the database refresh preserves a couple of real inboxes among otherwise anonymised users - so test sends never
/// reach real people. Leave the section absent (or the list empty) in production for unrestricted delivery.
/// The on/off master switch is separate and lives in the database (see EmailSettings) so it can be toggled at
/// runtime from the admin UI.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class EmailDeliverySettings
{
    public string[]? AllowedRecipients { get; init; }
}
