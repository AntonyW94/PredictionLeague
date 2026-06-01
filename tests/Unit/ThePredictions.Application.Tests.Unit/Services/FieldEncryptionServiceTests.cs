using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

public class FieldEncryptionServiceTests
{
    private static FieldEncryptionService CreateService(string? key = null)
    {
        key ??= Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        return new FieldEncryptionService(Options.Create(new FieldEncryptionSettings { Key = key }));
    }

    [Fact]
    public void EncryptThenDecrypt_ShouldReturnOriginal_WhenRoundTripped()
    {
        var service = CreateService();
        const string plaintext = "Mr A Willson|12-34-56|12345678";

        var cipher = service.Encrypt(plaintext);
        var result = service.Decrypt(cipher);

        result.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_ShouldReturnNull_WhenInputIsNull()
    {
        CreateService().Encrypt(null).Should().BeNull();
    }

    [Fact]
    public void Decrypt_ShouldReturnNull_WhenInputIsNull()
    {
        CreateService().Decrypt(null).Should().BeNull();
    }

    [Fact]
    public void EncryptThenDecrypt_ShouldRoundTrip_WhenEmptyString()
    {
        var service = CreateService();

        var cipher = service.Encrypt(string.Empty);

        service.Decrypt(cipher).Should().Be(string.Empty);
    }

    [Fact]
    public void Encrypt_ShouldProduceVersionedCipher_ThatDoesNotContainPlaintext()
    {
        var cipher = CreateService().Encrypt("12345678");

        cipher.Should().StartWith("v1:");
        cipher.Should().NotContain("12345678");
    }

    [Fact]
    public void Encrypt_ShouldProduceDifferentCiphertext_ForSamePlaintext()
    {
        var service = CreateService();

        var first = service.Encrypt("same-value");
        var second = service.Encrypt("same-value");

        first.Should().NotBe(second);
        service.Decrypt(first).Should().Be("same-value");
        service.Decrypt(second).Should().Be("same-value");
    }

    [Fact]
    public void Decrypt_ShouldThrow_WhenCiphertextTampered()
    {
        var service = CreateService();
        var cipher = service.Encrypt("secret")!;
        var payload = cipher["v1:".Length..];
        var tampered = "v1:" + (payload[0] == 'A' ? 'B' : 'A') + payload[1..];

        var act = () => service.Decrypt(tampered);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_ShouldThrowFormat_WhenNoVersionSeparator()
    {
        var act = () => CreateService().Decrypt("no-separator-here");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decrypt_ShouldThrowNotSupported_WhenVersionUnknown()
    {
        var act = () => CreateService().Decrypt("v2:AAAAAAAA");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Decrypt_ShouldThrowFormat_WhenPayloadTooShort()
    {
        var act = () => CreateService().Decrypt("v1:" + Convert.ToBase64String(new byte[4]));

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenKeyMissing()
    {
        var act = () => new FieldEncryptionService(Options.Create(new FieldEncryptionSettings { Key = null }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenKeyNotBase64()
    {
        var act = () => new FieldEncryptionService(Options.Create(new FieldEncryptionSettings { Key = "not valid base64 %%%" }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenKeyWrongLength()
    {
        var shortKey = Convert.ToBase64String(new byte[16]);

        var act = () => new FieldEncryptionService(Options.Create(new FieldEncryptionSettings { Key = shortKey }));

        act.Should().Throw<InvalidOperationException>();
    }
}
