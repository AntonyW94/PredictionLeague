using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Account.Commands;
using ThePredictions.Application.Repositories;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Account.Commands;

/// <summary>
/// Removing the bank details held against an account.
/// </summary>
public class DeletePayoutDetailsCommandHandlerTests
{
    private const string UserId = "user-1";

    private readonly IUserPayoutDetailsRepository _repository = Substitute.For<IUserPayoutDetailsRepository>();
    private readonly DeletePayoutDetailsCommandHandler _handler;

    public DeletePayoutDetailsCommandHandlerTests()
    {
        _handler = new DeletePayoutDetailsCommandHandler(_repository);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRefuseAnEmptyUser(string? userId)
    {
        // Without this guard a blank id would reach the delete and could match nothing - or worse.
        var act = () => _handler.Handle(new DeletePayoutDetailsCommand(userId!), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRemoveTheDetails()
    {
        await _handler.Handle(new DeletePayoutDetailsCommand(UserId), CancellationToken.None);

        await _repository.Received(1).DeleteAsync(UserId, Arg.Any<CancellationToken>());
    }
}
