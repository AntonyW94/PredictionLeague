using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Behaviours;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Behaviours;

/// <summary>
/// Runs every validator registered for a request before its handler sees it. A request with no
/// validator goes straight through, and one that fails never reaches the handler at all.
/// </summary>
public class ValidationBehaviourTests
{
    public record SampleCommand(string Name) : IRequest<string>;

    private readonly ILogger<ValidationBehaviour<SampleCommand, string>> _logger =
        Substitute.For<ILogger<ValidationBehaviour<SampleCommand, string>>>();

    private static IValidator<SampleCommand> Validator(params string[] errors)
    {
        var validator = Substitute.For<IValidator<SampleCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<SampleCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(errors.Select(e => new ValidationFailure("Name", e))));
        return validator;
    }

    private ValidationBehaviour<SampleCommand, string> Behaviour(params IValidator<SampleCommand>[] validators) =>
        new(validators, _logger);

    private static Task<string> Run(ValidationBehaviour<SampleCommand, string> behaviour, RequestHandlerDelegate<string>? next = null) =>
        behaviour.Handle(new SampleCommand("Alice"), next ?? (_ => Task.FromResult("done")), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRunTheHandler_WhenTheRequestHasNoValidators()
    {
        // Most requests have no validator at all, so this is the common path.
        var result = await Run(Behaviour());

        result.Should().Be("done");
    }

    [Fact]
    public async Task Handle_ShouldRunTheHandler_WhenEveryValidatorPasses()
    {
        var result = await Run(Behaviour(Validator(), Validator()));

        result.Should().Be("done");
    }

    [Fact]
    public async Task Handle_ShouldStopTheRequestReachingTheHandler_WhenValidationFails()
    {
        var handlerRan = false;

        var act = () => Run(Behaviour(Validator("Name is required")), _ =>
        {
            handlerRan = true;
            return Task.FromResult("done");
        });

        await act.Should().ThrowAsync<ValidationException>();
        handlerRan.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportEveryProblemAtOnce()
    {
        // Reporting one error at a time would make the user fix and resubmit repeatedly, so every
        // validator runs and the failures are gathered together.
        var act = () => Run(Behaviour(Validator("Name is required"), Validator("Name is too long")));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Select(e => e.ErrorMessage)
            .Should().BeEquivalentTo("Name is required", "Name is too long");
    }

    [Fact]
    public async Task Handle_ShouldStillFail_WhenOnlyOneOfSeveralValidatorsObjects()
    {
        var act = () => Run(Behaviour(Validator(), Validator("Name is required")));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ShouldRecordTheFailure()
    {
        var act = () => Run(Behaviour(Validator("Name is required")));

        await act.Should().ThrowAsync<ValidationException>();
        _logger.ReceivedWithAnyArgs().Log(default, default, default!, default, default!);
    }
}
