using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;

namespace ThePredictions.Application.Services;

/// <summary>
/// AES-GCM field encryption. Ciphertext format is "<version>:<base64(nonce|tag|ciphertext)>".
/// The version prefix lets the key be rotated later (a new version can decrypt old payloads).
/// </summary>
public class FieldEncryptionService : IFieldEncryptionService
{
    private const string CurrentVersion = "v1";
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBytes = 32;

    private readonly byte[] _key;

    public FieldEncryptionService(IOptions<FieldEncryptionSettings> options)
    {
        var configuredKey = options.Value.Key;

        if (string.IsNullOrWhiteSpace(configuredKey))
            throw new InvalidOperationException("Field encryption key is not configured (FieldEncryption:Key).");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Field encryption key must be a valid base64 string.");
        }

        if (key.Length != KeySizeBytes)
            throw new InvalidOperationException($"Field encryption key must be {KeySizeBytes} bytes (256-bit) when base64-decoded.");

        _key = key;
    }

    public string? Encrypt(string? plaintext)
    {
        if (plaintext is null)
            return null;

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);

        var payload = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, payload, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

        return $"{CurrentVersion}:{Convert.ToBase64String(payload)}";
    }

    public string? Decrypt(string? ciphertext)
    {
        if (ciphertext is null)
            return null;

        var separatorIndex = ciphertext.IndexOf(':');
        if (separatorIndex <= 0)
            throw new FormatException("Ciphertext is not in the expected '<version>:<payload>' format.");

        var version = ciphertext[..separatorIndex];
        if (version != CurrentVersion)
            throw new NotSupportedException($"Unsupported field encryption version '{version}'.");

        var payload = Convert.FromBase64String(ciphertext[(separatorIndex + 1)..]);
        if (payload.Length < NonceSizeBytes + TagSizeBytes)
            throw new FormatException("Ciphertext payload is too short.");

        var nonce = new byte[NonceSizeBytes];
        var tag = new byte[TagSizeBytes];
        var cipherBytes = new byte[payload.Length - NonceSizeBytes - TagSizeBytes];

        Buffer.BlockCopy(payload, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(payload, NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(payload, NonceSizeBytes + TagSizeBytes, cipherBytes, 0, cipherBytes.Length);

        var plaintextBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
