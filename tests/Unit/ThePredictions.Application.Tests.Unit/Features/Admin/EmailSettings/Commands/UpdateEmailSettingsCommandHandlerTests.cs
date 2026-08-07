using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.EmailSettings.Commands;
using ThePredictions.Application.Repositories;
using Xunit;
using DomainEmailSettings = ThePredictions.Domain.Models.EmailSettings;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.EmailSettings.Commands;

/// <summary>
/// The master switch that stops every outgoing email. Saving has to work on a database where the
/// row was never seeded, so the first save creates it - otherwise the switch could not be thrown at
/// all on a fresh environment.
/// </summary>
public class UpdateEmailSettingsCommandHandlerTests
{
    private readonly IEmailSettingsRepository _repository = Substitute.For<IEmailSettingsRepository>();
    private readonly UpdateEmailSettingsCommandHandler _handler;

    public UpdateEmailSettingsCommandHandlerTests()
    {
        _handler = new UpdateEmailSettingsCommandHandler(_repository);
    }

    private DomainEmailSettings GivenExisting(bool emailsEnabled)
    {
        var settings = new DomainEmailSettings(id: 1, emailsEnabled: emailsEnabled);
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        return settings;
    }

    private Task HandleAsync(bool emailsEnabled) =>
        _handler.Handle(new UpdateEmailSettingsCommand(emailsEnabled), CancellationToken.None);

    private DomainEmailSettings CapturedNewSettings() =>
        (DomainEmailSettings)_repository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IEmailSettingsRepository.AddAsync))
            .GetArguments()[0]!;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldSeedTheRowWithTheRequestedSetting_WhenNoneExistsYet(bool emailsEnabled)
    {
        // Both directions matter: the row starts from the built-in default, so turning emails off on
        // a fresh environment must not be silently overwritten by that default.
        await HandleAsync(emailsEnabled);

        CapturedNewSettings().EmailsEnabled.Should().Be(emailsEnabled);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldEditTheExistingRowInPlace(bool emailsEnabled)
    {
        var existing = GivenExisting(!emailsEnabled);

        await HandleAsync(emailsEnabled);

        existing.EmailsEnabled.Should().Be(emailsEnabled);
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
    }
}
