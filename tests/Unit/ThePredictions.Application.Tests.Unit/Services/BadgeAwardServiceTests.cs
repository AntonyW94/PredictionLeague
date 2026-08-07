using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

/// <summary>
/// Awards a badge, dated now. Callers fire this straight after the qualifying action without
/// checking first, so it has to be safe to call when the badge is already held.
/// </summary>
public class BadgeAwardServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly IUserBadgeRepository _repository = Substitute.For<IUserBadgeRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly BadgeAwardService _service;

    public BadgeAwardServiceTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _service = new BadgeAwardService(_repository, _dateTimeProvider);
    }

    [Fact]
    public async Task AwardAsync_ShouldAwardTheBadgeDatedNow()
    {
        await _service.AwardAsync("user-1", BadgeKeys.Banked, CancellationToken.None);

        await _repository.Received(1).AwardAsync(
            Arg.Is<AwardedBadge>(b => b.UserId == "user-1" && b.BadgeKey == BadgeKeys.Banked && b.AwardedUtc == NowUtc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AwardAsync_ShouldNotComplain_WhenTheBadgeIsAlreadyHeld()
    {
        // Callers award unconditionally after the qualifying action, so a repeat is normal.
        _repository.AwardAsync(Arg.Any<AwardedBadge>(), Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _service.AwardAsync("user-1", BadgeKeys.Banked, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
