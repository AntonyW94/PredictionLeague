using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using ThePredictions.Application.Services;
using ThePredictions.Infrastructure.Services;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// The master email switch is read on every send, so it is cached. If the cache stopped working the
/// site would hit the database once per email; if the fallback were wrong, a fresh environment with
/// no row seeded would silently send nothing.
/// </summary>
public class CachedEmailSettingsProviderTests
{
    private readonly IEmailSettingsQuery _settingsQuery = Substitute.For<IEmailSettingsQuery>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private CachedEmailSettingsProvider BuildProvider() => new(_settingsQuery, _cache);

    private void GivenStoredSetting(bool? enabled) =>
        _settingsQuery.GetEmailsEnabledAsync(Arg.Any<CancellationToken>()).Returns(enabled);

    [Fact]
    public async Task AreEmailsEnabledAsync_ShouldReturnTheStoredSetting_WhenEmailsAreOn()
    {
        GivenStoredSetting(true);

        var result = await BuildProvider().AreEmailsEnabledAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreEmailsEnabledAsync_ShouldReturnTheStoredSetting_WhenEmailsAreOff()
    {
        GivenStoredSetting(false);

        var result = await BuildProvider().AreEmailsEnabledAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AreEmailsEnabledAsync_ShouldFallBackToTheBuiltInDefault_WhenNoRowIsSeeded()
    {
        GivenStoredSetting(null);

        var result = await BuildProvider().AreEmailsEnabledAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreEmailsEnabledAsync_ShouldHitTheDatabaseOnlyOnce_WhileTheValueIsCached()
    {
        GivenStoredSetting(false);
        var provider = BuildProvider();

        await provider.AreEmailsEnabledAsync(CancellationToken.None);
        var second = await provider.AreEmailsEnabledAsync(CancellationToken.None);

        second.Should().BeFalse();
        await _settingsQuery.Received(1).GetEmailsEnabledAsync(Arg.Any<CancellationToken>());
    }
}
