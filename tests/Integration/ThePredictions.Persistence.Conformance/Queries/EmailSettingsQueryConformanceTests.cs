using FluentAssertions;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IEmailSettingsQuery"/> implementation must return.
///
/// The distinction that matters is between "switched off" and "never configured": both are falsy if an adapter is
/// careless, and they mean opposite things. A fresh environment with no settings row must report <c>null</c> so the
/// provider can fall back to emails being on - reporting <c>false</c> would silently stop every email the site sends.
/// </summary>
public abstract class EmailSettingsQueryConformanceTests
{
    protected abstract IEmailSettingsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task GetEmailsEnabledAsync_ShouldReturnNull_WhenNoSettingsRowExists()
    {
        // Act
        var enabled = await Query.GetEmailsEnabledAsync(CancellationToken.None);

        // Assert - not false. The absence of a decision is not a decision to send nothing.
        enabled.Should().BeNull();
    }

    [Fact]
    public async Task GetEmailsEnabledAsync_ShouldReturnTrue_WhenEmailsAreSwitchedOn()
    {
        // Arrange
        await Seed.AddEmailSettingsAsync(emailsEnabled: true);

        // Act
        var enabled = await Query.GetEmailsEnabledAsync(CancellationToken.None);

        // Assert
        enabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetEmailsEnabledAsync_ShouldReturnFalse_WhenEmailsAreSwitchedOff()
    {
        // Arrange
        await Seed.AddEmailSettingsAsync(emailsEnabled: false);

        // Act
        var enabled = await Query.GetEmailsEnabledAsync(CancellationToken.None);

        // Assert
        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetEmailsEnabledAsync_ShouldReadTheFirstRow_WhenMoreThanOneSomehowExists()
    {
        // Arrange - the table is meant to hold one row. If a second appears, the answer must still be predictable
        // rather than whichever the engine happened to return first.
        await Seed.AddEmailSettingsAsync(emailsEnabled: false);
        await Seed.AddEmailSettingsAsync(emailsEnabled: true);

        // Act
        var enabled = await Query.GetEmailsEnabledAsync(CancellationToken.None);

        // Assert
        enabled.Should().BeFalse();
    }
}
