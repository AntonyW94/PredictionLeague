using FluentAssertions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class EmailConfirmationTokenTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_ShouldSetProperties_AndDefaultTo72HourExpiry()
    {
        var token = EmailConfirmationToken.Create("abc123", "user-1", _dateTimeProvider);

        token.Token.Should().Be("abc123");
        token.UserId.Should().Be("user-1");
        token.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        token.ExpiresAtUtc.Should().Be(_dateTimeProvider.UtcNow.AddHours(72));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrow_WhenTokenBlank(string token)
    {
        var act = () => EmailConfirmationToken.Create(token, "user-1", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrow_WhenUserIdBlank(string userId)
    {
        var act = () => EmailConfirmationToken.Create("abc123", userId, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsExpired_ShouldBeFalse_BeforeExpiry()
    {
        var token = EmailConfirmationToken.Create("abc123", "user-1", _dateTimeProvider);

        token.IsExpired(_dateTimeProvider).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldBeTrue_AfterExpiry()
    {
        var token = EmailConfirmationToken.Create("abc123", "user-1", _dateTimeProvider);
        var later = new TestDateTimeProvider(_dateTimeProvider.UtcNow.AddHours(73));

        token.IsExpired(later).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var created = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var expires = created.AddHours(72);

        var token = new EmailConfirmationToken("tok", "user-9", created, expires);

        token.Token.Should().Be("tok");
        token.UserId.Should().Be("user-9");
        token.CreatedAtUtc.Should().Be(created);
        token.ExpiresAtUtc.Should().Be(expires);
    }
}
