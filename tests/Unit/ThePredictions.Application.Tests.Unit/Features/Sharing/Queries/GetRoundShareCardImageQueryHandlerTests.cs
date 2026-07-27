using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Sharing.Queries;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Sharing.Queries;

public class GetRoundShareCardImageQueryHandlerTests
{
    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IShareCardRenderer _renderer = Substitute.For<IShareCardRenderer>();
    private readonly GetRoundShareCardImageQueryHandler _handler;

    public GetRoundShareCardImageQueryHandlerTests()
    {
        _handler = new GetRoundShareCardImageQueryHandler(_dbConnection, _renderer);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullAndNotRender_WhenRoundDoesNotExist()
    {
        // The round lookup (QuerySingleOrDefaultAsync) returns null by default, standing in for a
        // round that does not exist or a user that is not found.
        var result = await _handler.Handle(new GetRoundShareCardImageQuery(99, "user-1", "dark"), CancellationToken.None);

        result.Should().BeNull();
        await _renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, CancellationToken.None);
    }
}
