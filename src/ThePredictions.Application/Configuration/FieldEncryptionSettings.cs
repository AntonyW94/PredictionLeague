using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class FieldEncryptionSettings
{
    public const string SectionName = "FieldEncryption";

    /// <summary>
    /// Base64-encoded 256-bit (32-byte) AES key. Supplied via Key Vault in dev/prod
    /// (it flows into configuration), or via user-secrets / appsettings.Local for local development.
    /// </summary>
    public string? Key { get; init; }
}
