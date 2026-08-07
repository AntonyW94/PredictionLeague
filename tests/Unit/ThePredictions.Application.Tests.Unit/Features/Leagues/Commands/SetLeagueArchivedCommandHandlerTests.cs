using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

/// <summary>
/// Hiding a finished league from your own dashboard. It only affects the person who asked - the
/// membership row is theirs - and nobody else's view of the league changes.
/// </summary>
public class SetLeagueArchivedCommandHandlerTests
{
    private const int LeagueId = 7;
    private const string UserId = "user-1";

    private static readonly DateTime JoinedAtUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueMemberRepository _repository = Substitute.For<ILeagueMemberRepository>();
    private readonly SetLeagueArchivedCommandHandler _handler;

    public SetLeagueArchivedCommandHandlerTests()
    {
        _handler = new SetLeagueArchivedCommandHandler(_repository);
    }

    private LeagueMember GivenMembership(LeagueMemberStatus status = LeagueMemberStatus.Approved, bool isArchived = false)
    {
        var member = new LeagueMember(leagueId: LeagueId, userId: UserId, status: status,
            isAlertDismissed: false, isArchivedByUser: isArchived, joinedAtUtc: JoinedAtUtc,
            approvedAtUtc: JoinedAtUtc, roundResults: null);

        _repository.GetAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(member);
        return member;
    }

    private Task HandleAsync(bool isArchived) =>
        _handler.Handle(new SetLeagueArchivedCommand(LeagueId, UserId, isArchived), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheyAreNotInThatLeague()
    {
        var act = () => HandleAsync(isArchived: true);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldArchiveTheLeagueForThem()
    {
        var member = GivenMembership();

        await HandleAsync(isArchived: true);

        member.IsArchivedByUser.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldBringAnArchivedLeagueBack()
    {
        var member = GivenMembership(isArchived: true);

        await HandleAsync(isArchived: false);

        member.IsArchivedByUser.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(member, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuseToArchive_WhileTheirRequestToJoinIsStillPending()
    {
        // There is nothing to hide yet, and archiving would take the league off the dashboard where
        // they are waiting to be let in.
        GivenMembership(LeagueMemberStatus.Pending);

        var act = () => HandleAsync(isArchived: true);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAllowUnarchivingWhateverTheirStanding()
    {
        // Unarchiving only ever restores the default view, so it carries no such restriction.
        var member = GivenMembership(LeagueMemberStatus.Pending, isArchived: true);

        await HandleAsync(isArchived: false);

        member.IsArchivedByUser.Should().BeFalse();
    }
}
