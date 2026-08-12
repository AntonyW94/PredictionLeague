using FluentAssertions;
using ThePredictions.Application.Features.Sharing.Queries;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IShareCardPlayerQuery"/> implementation must return: the player a share card is for, or nothing.
/// </summary>
public abstract class ShareCardPlayerQueryConformanceTests
{
    protected abstract IShareCardPlayerQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForAnIdThatMatchesNoPlayer()
    {
        // What to draw instead - an unnamed card in the default theme - is the handler's rule.
        (await Query.ExecuteAsync("no-such-user", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheFirstNameAndSavedTheme()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var player = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the theme arrives as stored, because how it interacts with the one the browser is showing is a rule.
        player.Should().NotBeNull();
        player!.FirstName.Should().Be("Ada");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheThemeExactlyAsStored()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var player = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - as stored, not translated. Which theme the card is drawn in depends on what the browser is showing as well
        // as what is saved, and only the handler knows both.
        player!.PreferredTheme.Should().Be("light");
    }
}
