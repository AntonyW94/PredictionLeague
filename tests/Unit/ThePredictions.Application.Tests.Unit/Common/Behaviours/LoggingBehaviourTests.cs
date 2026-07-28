using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Behaviours;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Behaviours;

public class LoggingBehaviourTests
{
    public record SampleCommand : IRequest<string>;

    public record SampleQuery : IRequest<string>;

    [Fact]
    public async Task Handle_ShouldLogInformation_WhenRequestIsACommand()
    {
        var logger = Substitute.For<ILogger<LoggingBehaviour<SampleCommand, string>>>();
        var behaviour = new LoggingBehaviour<SampleCommand, string>(logger);

        var result = await behaviour.Handle(new SampleCommand(), _ => Task.FromResult("done"), CancellationToken.None);

        result.Should().Be("done");
        logger.ReceivedWithAnyArgs(1).Log(default, default, default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldNotLog_WhenRequestIsAQuery()
    {
        var logger = Substitute.For<ILogger<LoggingBehaviour<SampleQuery, string>>>();
        var behaviour = new LoggingBehaviour<SampleQuery, string>(logger);

        var result = await behaviour.Handle(new SampleQuery(), _ => Task.FromResult("done"), CancellationToken.None);

        result.Should().Be("done");
        logger.DidNotReceiveWithAnyArgs().Log(default, default, default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldLogAndRethrow_WhenTheCommandThrows()
    {
        var logger = Substitute.For<ILogger<LoggingBehaviour<SampleCommand, string>>>();
        var behaviour = new LoggingBehaviour<SampleCommand, string>(logger);

        var act = async () => await behaviour.Handle(
            new SampleCommand(),
            _ => throw new InvalidOperationException("nope"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("nope");
        logger.ReceivedWithAnyArgs(1).Log(default, default, default!, default, default!);
    }
}
