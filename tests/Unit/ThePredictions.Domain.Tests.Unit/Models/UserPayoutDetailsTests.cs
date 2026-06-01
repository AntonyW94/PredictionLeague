using FluentAssertions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class UserPayoutDetailsTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_ShouldSetPropertiesAndTimestamps()
    {
        var details = UserPayoutDetails.Create("user-1", "enc-name", "enc-sort", "enc-account", _dateTimeProvider);

        details.UserId.Should().Be("user-1");
        details.AccountName.Should().Be("enc-name");
        details.SortCode.Should().Be("enc-sort");
        details.AccountNumber.Should().Be("enc-account");
        details.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        details.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldThrow_WhenUserIdBlank()
    {
        var act = () => UserPayoutDetails.Create("  ", "n", "s", "a", _dateTimeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HasDetails_ShouldBeTrue_WhenAllFieldsSet()
    {
        var details = UserPayoutDetails.Create("user-1", "enc-name", "enc-sort", "enc-account", _dateTimeProvider);

        details.HasDetails.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "enc-sort", "enc-account")]
    [InlineData("enc-name", null, "enc-account")]
    [InlineData("enc-name", "enc-sort", null)]
    [InlineData(null, null, null)]
    public void HasDetails_ShouldBeFalse_WhenAnyFieldMissing(string? accountName, string? sortCode, string? accountNumber)
    {
        var details = UserPayoutDetails.Create("user-1", accountName, sortCode, accountNumber, _dateTimeProvider);

        details.HasDetails.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldReplaceFieldsAndBumpUpdatedAt()
    {
        var details = UserPayoutDetails.Create("user-1", "enc-name", "enc-sort", "enc-account", _dateTimeProvider);
        _dateTimeProvider.AdvanceBy(TimeSpan.FromDays(2));

        details.Update("enc-name-2", "enc-sort-2", "enc-account-2", _dateTimeProvider);

        details.AccountName.Should().Be("enc-name-2");
        details.SortCode.Should().Be("enc-sort-2");
        details.AccountNumber.Should().Be("enc-account-2");
        details.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        details.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow.AddDays(-2));
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabaseValues()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var details = new UserPayoutDetails("user-9", "enc-n", "enc-s", "enc-a", created, updated);

        details.UserId.Should().Be("user-9");
        details.AccountName.Should().Be("enc-n");
        details.SortCode.Should().Be("enc-s");
        details.AccountNumber.Should().Be("enc-a");
        details.CreatedAtUtc.Should().Be(created);
        details.UpdatedAtUtc.Should().Be(updated);
        details.HasDetails.Should().BeTrue();
    }
}
