using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class NotifyLeagueAdminOfJoinRequestCommandHandlerTests
{
    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly NotifyLeagueAdminOfJoinRequestCommandHandler _handler;

    private readonly BrevoSettings _brevoSettings = new()
    {
        Templates = new TemplateSettings
        {
            JoinLeagueRequest = 300
        }
    };

    public NotifyLeagueAdminOfJoinRequestCommandHandlerTests()
    {
        var options = Options.Create(_brevoSettings);
        _handler = new NotifyLeagueAdminOfJoinRequestCommandHandler(_dbConnection, _emailService, options);
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenAdminIsFound()
    {
        // Arrange
        var command = new NotifyLeagueAdminOfJoinRequestCommand("admin-user", "Test League", 1, "Jane", "Doe");
        var adminDto = new LeagueAdminDto("admin@example.com", "Admin", "2025/26");

        _dbConnection.QuerySingleOrDefaultAsync<LeagueAdminDto>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object>())
            .Returns(adminDto);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendTemplatedEmailAsync(
            "admin@example.com", 300, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenAdminNotFound()
    {
        // Arrange
        var command = new NotifyLeagueAdminOfJoinRequestCommand("admin-user", "Test League", 1, "Jane", "Doe");

        _dbConnection.QuerySingleOrDefaultAsync<LeagueAdminDto>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object>())
            .Returns((LeagueAdminDto?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenTemplatesNotConfigured()
    {
        // Arrange
        var settingsWithoutTemplates = new BrevoSettings { Templates = null };
        var options = Options.Create(settingsWithoutTemplates);
        var handler = new NotifyLeagueAdminOfJoinRequestCommandHandler(_dbConnection, _emailService, options);
        var command = new NotifyLeagueAdminOfJoinRequestCommand("admin-user", "Test League", 1, "Jane", "Doe");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }
}
