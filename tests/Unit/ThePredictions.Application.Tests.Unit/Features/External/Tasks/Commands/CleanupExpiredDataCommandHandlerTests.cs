using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.External.Tasks.Commands;
using ThePredictions.Application.Repositories;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.External.Tasks.Commands;

/// <summary>
/// The scheduled sweep that clears out spent password-reset tokens. Keeping them indefinitely would
/// leave usable-looking secrets lying around long after they stopped working.
/// </summary>
public class CleanupExpiredDataCommandHandlerTests
{
    private readonly IPasswordResetTokenRepository _repository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly ILogger<CleanupExpiredDataCommandHandler> _logger = Substitute.For<ILogger<CleanupExpiredDataCommandHandler>>();

    private readonly CleanupExpiredDataCommandHandler _handler;

    public CleanupExpiredDataCommandHandlerTests()
    {
        _handler = new CleanupExpiredDataCommandHandler(_repository, _logger);
    }

    private Task<CleanupResult> HandleAsync() =>
        _handler.Handle(new CleanupExpiredDataCommand(), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldOnlyRemoveTokensOlderThanThirtyDays()
    {
        // Recent tokens may still be in someone's inbox waiting to be used.
        var before = DateTime.UtcNow;

        await HandleAsync();

        var after = DateTime.UtcNow;
        await _repository.Received(1).DeleteTokensOlderThanAsync(
            Arg.Is<DateTime>(cutoff => cutoff >= before.AddDays(-30) && cutoff <= after.AddDays(-30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReportHowManyWereRemoved()
    {
        _repository.DeleteTokensOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(7);

        var result = await HandleAsync();

        result.PasswordResetTokensDeleted.Should().Be(7);
    }

    [Fact]
    public async Task Handle_ShouldReportZero_WhenThereWasNothingToRemove()
    {
        // The sweep runs on a schedule, so most runs find nothing and must still succeed.
        _repository.DeleteTokensOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await HandleAsync();

        result.PasswordResetTokensDeleted.Should().Be(0);
    }
}
