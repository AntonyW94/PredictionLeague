namespace ThePredictions.Application.Services;

/// <summary>
/// Application-level field encryption for sensitive data stored at rest (e.g. league bank details).
/// Null in, null out, so optional fields round-trip cleanly. Never log plaintext.
/// </summary>
public interface IFieldEncryptionService
{
    /// <summary>Encrypts a plaintext value, returning a versioned ciphertext string. Returns null for null input.</summary>
    string? Encrypt(string? plaintext);

    /// <summary>Decrypts a versioned ciphertext value produced by <see cref="Encrypt"/>. Returns null for null input.</summary>
    string? Decrypt(string? ciphertext);
}
