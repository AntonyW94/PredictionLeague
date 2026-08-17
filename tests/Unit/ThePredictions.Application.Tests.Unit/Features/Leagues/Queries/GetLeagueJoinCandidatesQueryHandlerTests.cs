using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The choices offered by the "Add Member" picker on the league members page.
/// </summary>
public class GetLeagueJoinCandidatesQueryHandlerTests
{
    private const int LeagueId = 42;

    private readonly ILeagueJoinCandidatesQuery _candidatesQuery = Substitute.For<ILeagueJoinCandidatesQuery>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly GetLeagueJoinCandidatesQueryHandler _handler;

    public GetLeagueJoinCandidatesQueryHandlerTests()
    {
        _handler = new GetLeagueJoinCandidatesQueryHandler(_candidatesQuery, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        // Arrange
        _currentUserService.When(service => service.EnsureAdministrator())
            .Throw(new UnauthorizedAccessException());

        // Act
        var act = () => HandleAsync();

        // Assert - this read is a list of pass holders' email addresses, gated the same way the command it feeds is
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _candidatesQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _candidatesQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<LeagueJoinCandidateRow>?)null);

        // Act
        var act = () => HandleAsync();

        // Assert - null is the read's way of saying "no such league", which an empty list cannot say
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyList_WhenEverybodyHasAlreadyJoined()
    {
        // Arrange
        Given();

        // Act
        var candidates = await HandleAsync();

        // Assert - a perfectly good state, and not the same answer as a missing league
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOrderCandidatesByFirstNameThenLast()
    {
        // Arrange
        Given(
            Candidate("u1", "Grace", "Hopper"),
            Candidate("u2", "Ada", "Turing"),
            Candidate("u3", "Ada", "Lovelace"));

        // Act
        var candidates = await HandleAsync();

        // Assert
        candidates.Select(candidate => candidate.FullName).Should().Equal("Ada Lovelace", "Ada Turing", "Grace Hopper");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheFullNameAndEmail()
    {
        // Arrange
        Given(Candidate("u1", "Ada", "Lovelace", "ada@example.com"));

        // Act
        var candidate = (await HandleAsync()).Single();

        // Assert - the full name, not the abbreviated "Ada L" other players see: an administrator picking the wrong
        // person cannot undo it, and the email is there for the two players who share a name.
        candidate.UserId.Should().Be("u1");
        candidate.FullName.Should().Be("Ada Lovelace");
        candidate.Email.Should().Be("ada@example.com");
    }

    private void Given(params LeagueJoinCandidateRow[] candidates)
    {
        _candidatesQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(candidates);
    }

    private async Task<List<LeagueJoinCandidateDto>> HandleAsync() =>
        await _handler.Handle(new GetLeagueJoinCandidatesQuery(LeagueId), CancellationToken.None);

    private static LeagueJoinCandidateRow Candidate(
        string userId,
        string firstName,
        string lastName,
        string email = "player@example.com") =>
        new(userId, firstName, lastName, email);
}
