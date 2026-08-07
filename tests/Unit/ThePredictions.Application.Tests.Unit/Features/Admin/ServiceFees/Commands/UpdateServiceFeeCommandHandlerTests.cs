using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.ServiceFees.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.ServiceFees.Commands;

/// <summary>
/// The card-processing fee used when working out what a season should cost. Saving it has to work
/// on a database where the row was never seeded, so the first save creates it.
/// </summary>
public class UpdateServiceFeeCommandHandlerTests
{
    private readonly IServiceFeeRepository _repository = Substitute.For<IServiceFeeRepository>();
    private readonly UpdateServiceFeeCommandHandler _handler;

    public UpdateServiceFeeCommandHandlerTests()
    {
        _handler = new UpdateServiceFeeCommandHandler(_repository);
    }

    private ServiceFee GivenExisting()
    {
        var fee = new ServiceFee(id: 1, provider: ServiceFeeProvider.Stripe, percentFee: 0.014m, fixedFee: 0.20m);
        _repository.GetByProviderAsync(ServiceFeeProvider.Stripe, Arg.Any<CancellationToken>()).Returns(fee);
        return fee;
    }

    private Task HandleAsync(decimal percentFee = 0.029m, decimal fixedFee = 0.30m) =>
        _handler.Handle(new UpdateServiceFeeCommand(ServiceFeeProvider.Stripe, percentFee, fixedFee), CancellationToken.None);

    private ServiceFee CapturedNewFee() =>
        (ServiceFee)_repository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IServiceFeeRepository.AddAsync))
            .GetArguments()[0]!;

    [Fact]
    public async Task Handle_ShouldSeedTheRowWithTheRequestedFigures_WhenNoneExistsYet()
    {
        // A fresh database has no fee row, so the first save must create one rather than fail - and
        // it must hold what was typed, not the built-in default it started from.
        await HandleAsync(percentFee: 0.029m, fixedFee: 0.30m);

        var stored = CapturedNewFee();
        stored.Provider.Should().Be(ServiceFeeProvider.Stripe);
        stored.PercentFee.Should().Be(0.029m);
        stored.FixedFee.Should().Be(0.30m);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldEditTheExistingRowInPlace()
    {
        var existing = GivenExisting();

        await HandleAsync(percentFee: 0.029m, fixedFee: 0.30m);

        existing.PercentFee.Should().Be(0.029m);
        existing.FixedFee.Should().Be(0.30m);
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
    }
}
