using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.PricingSettings.Commands;
using ThePredictions.Application.Repositories;
using Xunit;
using DomainPricingSettings = ThePredictions.Domain.Models.PricingSettings;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.PricingSettings.Commands;

/// <summary>
/// The safety margin and price floor used when suggesting what a season should cost. Saving has to
/// work on a database where the row was never seeded, so the first save creates it.
/// </summary>
public class UpdatePricingSettingsCommandHandlerTests
{
    private readonly IPricingSettingsRepository _repository = Substitute.For<IPricingSettingsRepository>();
    private readonly UpdatePricingSettingsCommandHandler _handler;

    public UpdatePricingSettingsCommandHandlerTests()
    {
        _handler = new UpdatePricingSettingsCommandHandler(_repository);
    }

    private DomainPricingSettings GivenExisting()
    {
        var settings = new DomainPricingSettings(id: 1, bufferRate: 0.10m, minimumFloor: 1m);
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        return settings;
    }

    private Task HandleAsync(decimal bufferRate = 0.25m, decimal minimumFloor = 2.50m) =>
        _handler.Handle(new UpdatePricingSettingsCommand(bufferRate, minimumFloor), CancellationToken.None);

    private DomainPricingSettings CapturedNewSettings() =>
        (DomainPricingSettings)_repository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IPricingSettingsRepository.AddAsync))
            .GetArguments()[0]!;

    [Fact]
    public async Task Handle_ShouldSeedTheRowWithTheRequestedFigures_WhenNoneExistsYet()
    {
        // The row starts from the built-in defaults but must end up holding what was typed.
        await HandleAsync(bufferRate: 0.25m, minimumFloor: 2.50m);

        var stored = CapturedNewSettings();
        stored.BufferRate.Should().Be(0.25m);
        stored.MinimumFloor.Should().Be(2.50m);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldEditTheExistingRowInPlace()
    {
        var existing = GivenExisting();

        await HandleAsync(bufferRate: 0.25m, minimumFloor: 2.50m);

        existing.BufferRate.Should().Be(0.25m);
        existing.MinimumFloor.Should().Be(2.50m);
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
    }
}
