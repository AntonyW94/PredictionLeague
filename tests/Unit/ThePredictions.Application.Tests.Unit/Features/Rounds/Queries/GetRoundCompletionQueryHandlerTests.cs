using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Rounds.Queries;

public class GetRoundCompletionQueryHandlerTests
{
    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetRoundCompletionQueryHandler _handler;

    public GetRoundCompletionQueryHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc));
        _handler = new GetRoundCompletionQueryHandler(_dbConnection, _membershipService, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorised_WhenGlobalViewRequestedByNonAdmin()
    {
        var query = new GetRoundCompletionQuery(43, LeagueId: null, "user-x", IsSiteAdmin: false);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldEnforceMembership_WhenLeagueViewRequestedByNonMember()
    {
        _membershipService.EnsureApprovedMemberAsync(10, "user-x", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));
        var query = new GetRoundCompletionQuery(43, LeagueId: 10, "user-x", IsSiteAdmin: false);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
