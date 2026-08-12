using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// One league's settings.
///
/// Three of its answers were <c>ISNULL</c> or <c>CASE</c> expressions standing in for something the database does not
/// store: the word "Public" for a league with no entry code, a date in 1900 for one with no deadline, and a flag for
/// whether the competition is a tournament.
/// </summary>
public class GetLeagueByIdQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private readonly ILeagueDetailQuery _detailQuery = Substitute.For<ILeagueDetailQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeagueByIdQueryHandler _handler;

    public GetLeagueByIdQueryHandlerTests()
    {
        _handler = new GetLeagueByIdQueryHandler(_detailQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _detailQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _detailQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((LeagueDetailRow?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheLeaguesSettings()
    {
        // Arrange
        Given(Row(
            name: "Test League",
            seasonName: "2026/27",
            price: 15m,
            pointsForExactScore: 5,
            pointsForCorrectResult: 2,
            requiresMemberApproval: false,
            isListed: true));

        // Act
        var league = await HandleAsync();

        // Assert
        league.Name.Should().Be("Test League");
        league.SeasonName.Should().Be("2026/27");
        league.Price.Should().Be(15m);
        league.PointsForExactScore.Should().Be(5);
        league.PointsForCorrectResult.Should().Be(2);
        league.RequiresMemberApproval.Should().BeFalse();
        league.IsListed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportTheEntryCode_WhenTheLeagueIsPrivate()
    {
        // Arrange
        Given(Row(entryCode: "SECRET"));

        // Act
        var league = await HandleAsync();

        // Assert
        league.EntryCode.Should().Be("SECRET");
    }

    [Fact]
    public async Task Handle_ShouldReportThePublicWord_WhenTheLeagueHasNoEntryCode()
    {
        // Arrange
        Given(Row(entryCode: null));

        // Act
        var league = await HandleAsync();

        // Assert - anyone may join, so there is nothing to type in.
        league.EntryCode.Should().Be("Public");
    }

    [Fact]
    public async Task Handle_ShouldReportTheEntryDeadline_WhenTheLeagueHasOne()
    {
        // Arrange
        var deadline = new DateTime(2026, 8, 14, 18, 30, 0, DateTimeKind.Utc);
        Given(Row(entryDeadlineUtc: deadline));

        // Act
        var league = await HandleAsync();

        // Assert
        league.EntryDeadlineUtc.Should().Be(deadline);
    }

    [Fact]
    public async Task Handle_ShouldReportNoEntryDeadline_WhenTheLeagueHasNotSetOne()
    {
        // Arrange
        Given(Row(entryDeadlineUtc: null));

        // Act
        var league = await HandleAsync();

        // Assert - "no deadline" rather than the 1st of January 1900. The contract already allowed null; this handler was
        // still overriding it with the sentinel the old ISNULL produced, so the page showed a date nobody set.
        league.EntryDeadlineUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(CompetitionType.Tournament, true)]
    [InlineData(CompetitionType.League, false)]
    public async Task Handle_ShouldReportWhetherTheCompetitionIsATournament(
        CompetitionType competitionType,
        bool expected)
    {
        // Arrange
        Given(Row(competitionType: competitionType));

        // Act
        var league = await HandleAsync();

        // Assert
        league.IsTournament.Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldReportWhetherAPrizeSchemeExists(bool hasPrizeScheme)
    {
        // Arrange
        Given(Row(hasPrizeScheme: hasPrizeScheme));

        // Act
        var league = await HandleAsync();

        // Assert
        league.HasPrizeScheme.Should().Be(hasPrizeScheme);
    }

    [Fact]
    public async Task Handle_ShouldCountEveryMembershipIncludingRequests()
    {
        // Arrange - eight memberships in total, of which five are approved.
        Given(Row(totalMembershipCount: 8, approvedMemberCount: 5));

        // Act
        var league = await HandleAsync();

        // Assert - preserved from the old COUNT over an unfiltered join, and the odd one out: every other member count
        // on the site counts approved members only. Flagged in the plan document as a question for the owner.
        league.MemberCount.Should().Be(8);
    }

    private void Given(LeagueDetailRow row)
    {
        _detailQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(row);
    }

    private async Task<LeagueDto> HandleAsync() =>
        await _handler.Handle(new GetLeagueByIdQuery(LeagueId, UserId), CancellationToken.None);

    private static LeagueDetailRow Row(
        string name = "Test League",
        string seasonName = "2026/27",
        int seasonId = 7,
        int totalMembershipCount = 5,
        int approvedMemberCount = 5,
        decimal price = 10m,
        string? entryCode = null,
        DateTime? entryDeadlineUtc = null,
        int pointsForExactScore = 3,
        int pointsForCorrectResult = 1,
        CompetitionType competitionType = CompetitionType.League,
        bool hasPrizeScheme = false,
        bool requiresMemberApproval = true,
        bool isListed = false) =>
        new(
            LeagueId,
            name,
            seasonName,
            seasonId,
            totalMembershipCount,
            approvedMemberCount,
            price,
            entryCode,
            entryDeadlineUtc,
            pointsForExactScore,
            pointsForCorrectResult,
            competitionType,
            hasPrizeScheme,
            requiresMemberApproval,
            isListed);
}
