using FluentAssertions;
using PredictionLeague.Domain.Common.Constants;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using ThePredictions.Domain.Tests.Unit.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeagueManagementTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    private Season CreateFutureSeason() =>
        new(id: 1, name: "2025/26", startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8), isActive: true, numberOfRounds: 38, apiLeagueId: null);

    private DateTime FutureDeadline => _dateTimeProvider.UtcNow.AddMonths(1);

    private League CreateLeagueViaFactory(
        int seasonId = 1,
        string name = "Test League",
        string administratorUserId = "admin-user",
        DateTime? entryDeadlineUtc = null,
        int pointsForExactScore = 3,
        int pointsForCorrectResult = 1,
        decimal price = 0m,
        Season? season = null)
    {
        return League.Create(
            seasonId,
            name,
            administratorUserId,
            entryDeadlineUtc ?? FutureDeadline,
            pointsForExactScore,
            pointsForCorrectResult,
            price,
            season ?? CreateFutureSeason(),
            _dateTimeProvider);
    }

    /// <summary>
    /// Creates a league with an explicit ID set (via the public/database constructor)
    /// for tests that call methods requiring a valid ID (e.g. AddMember, RemoveMember).
    /// </summary>
    private League CreateLeagueWithId(int id = 1)
    {
        return new League(
            id: id, name: "Test League", seasonId: 1,
            administratorUserId: "admin-user", entryCode: "ABC123",
            createdAtUtc: _dateTimeProvider.UtcNow,
            entryDeadlineUtc: FutureDeadline,
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null);
    }

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateLeague_WhenValidParametersProvided()
    {
        // Act
        var league = CreateLeagueViaFactory();

        // Assert
        league.SeasonId.Should().Be(1);
        league.Name.Should().Be("Test League");
        league.AdministratorUserId.Should().Be("admin-user");
        league.PointsForExactScore.Should().Be(3);
        league.PointsForCorrectResult.Should().Be(1);
    }

    [Fact]
    public void Create_ShouldSetIsFreeTrue_WhenPriceIsZero()
    {
        // Act
        var league = CreateLeagueViaFactory(price: 0m);

        // Assert
        league.IsFree.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldSetIsFreeFalse_WhenPriceIsPositive()
    {
        // Act
        var league = CreateLeagueViaFactory(price: 10m);

        // Assert
        league.IsFree.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetIsFreeFalse_WhenPriceIsMinimalPositive()
    {
        // Act
        var league = CreateLeagueViaFactory(price: 0.01m);

        // Assert
        league.IsFree.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetCreatedAtUtc_WhenCreated()
    {
        // Act
        var league = CreateLeagueViaFactory();

        // Assert
        league.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldSetEntryCodeToNull_WhenCreated()
    {
        // Act
        var league = CreateLeagueViaFactory();

        // Assert
        league.EntryCode.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetHasPrizesToFalse_WhenCreated()
    {
        // Act
        var league = CreateLeagueViaFactory();

        // Assert
        league.HasPrizes.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetPrizeFundOverrideToNull_WhenCreated()
    {
        // Act
        var league = CreateLeagueViaFactory();

        // Assert
        league.PrizeFundOverride.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetPointsForExactScore_WhenProvided()
    {
        // Act
        var league = CreateLeagueViaFactory(pointsForExactScore: 5);

        // Assert
        league.PointsForExactScore.Should().Be(5);
    }

    [Fact]
    public void Create_ShouldSetPointsForCorrectResult_WhenProvided()
    {
        // Act
        var league = CreateLeagueViaFactory(pointsForCorrectResult: 3);

        // Assert
        league.PointsForCorrectResult.Should().Be(3);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsNull()
    {
        // Act
        var act = () => CreateLeagueViaFactory(name: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsEmpty()
    {
        // Act
        var act = () => CreateLeagueViaFactory(name: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsWhitespace()
    {
        // Act
        var act = () => CreateLeagueViaFactory(name: " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAdministratorUserIdIsNull()
    {
        // Act
        var act = () => CreateLeagueViaFactory(administratorUserId: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAdministratorUserIdIsEmpty()
    {
        // Act
        var act = () => CreateLeagueViaFactory(administratorUserId: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAdministratorUserIdIsWhitespace()
    {
        // Act
        var act = () => CreateLeagueViaFactory(administratorUserId: " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenSeasonIdIsZero()
    {
        // Act
        var act = () => CreateLeagueViaFactory(seasonId: 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenSeasonIdIsNegative()
    {
        // Act
        var act = () => CreateLeagueViaFactory(seasonId: -1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEntryDeadlineIsInThePast()
    {
        // Act
        var act = () => CreateLeagueViaFactory(entryDeadlineUtc: _dateTimeProvider.UtcNow.AddDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEntryDeadlineIsAfterSeasonStart()
    {
        // Arrange — deadline after season start
        var season = CreateFutureSeason();

        // Act
        var act = () => CreateLeagueViaFactory(
            entryDeadlineUtc: season.StartDateUtc.AddDays(1),
            season: season);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEntryDeadlineEqualToSeasonStart()
    {
        // Arrange — deadline same date as season start
        var season = CreateFutureSeason();

        // Act
        var act = () => CreateLeagueViaFactory(
            entryDeadlineUtc: season.StartDateUtc,
            season: season);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateOfficialPublicLeague

    [Fact]
    public void CreateOfficialPublicLeague_ShouldSetNameWithOfficialPrefix()
    {
        // Act
        var league = League.CreateOfficialPublicLeague(
            1, "2025/26", 10m, "admin-user", FutureDeadline, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.Name.Should().Be("Official 2025/26 League");
    }

    [Fact]
    public void CreateOfficialPublicLeague_ShouldUsePublicLeagueSettings()
    {
        // Act
        var league = League.CreateOfficialPublicLeague(
            1, "2025/26", 10m, "admin-user", FutureDeadline, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.PointsForExactScore.Should().Be(PublicLeagueSettings.PointsForExactScore);
        league.PointsForCorrectResult.Should().Be(PublicLeagueSettings.PointsForCorrectResult);
    }

    [Fact]
    public void CreateOfficialPublicLeague_ShouldSetPriceCorrectly()
    {
        // Act
        var league = League.CreateOfficialPublicLeague(
            1, "2025/26", 10m, "admin-user", FutureDeadline, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.Price.Should().Be(10m);
    }

    [Fact]
    public void CreateOfficialPublicLeague_ShouldThrowException_WhenDeadlineIsInThePast()
    {
        // Act
        var act = () => League.CreateOfficialPublicLeague(
            1, "2025/26", 10m, "admin-user", _dateTimeProvider.UtcNow.AddDays(-1), CreateFutureSeason(), _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region SetEntryCode

    [Fact]
    public void SetEntryCode_ShouldSetEntryCode_WhenValidCodeProvided()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        league.SetEntryCode("ABC123");

        // Assert
        league.EntryCode.Should().Be("ABC123");
    }

    [Fact]
    public void SetEntryCode_ShouldAcceptAllUppercaseLetters_WhenProvided()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("ABCDEF");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetEntryCode_ShouldAcceptAllDigits_WhenProvided()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("123456");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeIsNull()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeIsEmpty()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeIsWhitespace()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode(" ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeIsTooShort()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("ABC12");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeIsTooLong()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("ABC1234");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeContainsLowercase()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("abc123");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeContainsSpecialCharacters()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("ABC!@#");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetEntryCode_ShouldThrowException_WhenCodeContainsSpaces()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.SetEntryCode("ABC 12");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region AddMember

    [Fact]
    public void AddMember_ShouldAddMember_WhenUserIdIsValid()
    {
        // Arrange
        var league = CreateLeagueWithId();

        // Act
        league.AddMember("user-1", _dateTimeProvider);

        // Assert
        league.Members.Should().HaveCount(1);
    }

    [Fact]
    public void AddMember_ShouldCreateMemberWithPendingStatus()
    {
        // Arrange
        var league = CreateLeagueWithId();

        // Act
        league.AddMember("user-1", _dateTimeProvider);

        // Assert
        league.Members.First().Status.Should().Be(LeagueMemberStatus.Pending);
    }

    [Fact]
    public void AddMember_ShouldAddMultipleMembers_WhenDifferentUserIds()
    {
        // Arrange
        var league = CreateLeagueWithId();

        // Act
        league.AddMember("user-1", _dateTimeProvider);
        league.AddMember("user-2", _dateTimeProvider);

        // Assert
        league.Members.Should().HaveCount(2);
    }

    [Fact]
    public void AddMember_ShouldThrowException_WhenUserIdIsNull()
    {
        // Arrange
        var league = CreateLeagueWithId();

        // Act
        var act = () => league.AddMember(null!, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddMember_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Arrange
        var league = CreateLeagueWithId();

        // Act
        var act = () => league.AddMember("", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddMember_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Arrange
        var league = CreateLeagueWithId();

        // Act
        var act = () => league.AddMember(" ", _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddMember_ShouldThrowException_WhenUserIsAlreadyMember()
    {
        // Arrange
        var league = CreateLeagueWithId();
        league.AddMember("user-1", _dateTimeProvider);

        // Act
        var act = () => league.AddMember("user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddMember_ShouldThrowException_WhenDeadlineHasPassed()
    {
        // Arrange — build league with past deadline via public constructor
        var league = new League(
            id: 1, name: "Test League", seasonId: 1,
            administratorUserId: "admin-user", entryCode: "ABC123",
            createdAtUtc: _dateTimeProvider.UtcNow.AddDays(-10),
            entryDeadlineUtc: _dateTimeProvider.UtcNow.AddDays(-1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null);

        // Act
        var act = () => league.AddMember("user-1", _dateTimeProvider);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region RemoveMember

    [Fact]
    public void RemoveMember_ShouldRemoveMember_WhenMemberExists()
    {
        // Arrange
        var league = CreateLeagueWithId();
        league.AddMember("user-1", _dateTimeProvider);

        // Act
        league.RemoveMember("user-1");

        // Assert
        league.Members.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMember_ShouldDoNothing_WhenMemberDoesNotExist()
    {
        // Arrange
        var league = CreateLeagueWithId();
        league.AddMember("user-1", _dateTimeProvider);

        // Act
        league.RemoveMember("non-existent");

        // Assert
        league.Members.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveMember_ShouldLeaveOtherMembers_WhenRemovingOne()
    {
        // Arrange
        var league = CreateLeagueWithId();
        league.AddMember("user-1", _dateTimeProvider);
        league.AddMember("user-2", _dateTimeProvider);

        // Act
        league.RemoveMember("user-1");

        // Assert
        league.Members.Should().HaveCount(1);
        league.Members.First().UserId.Should().Be("user-2");
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateAllProperties_WhenValid()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var season = CreateFutureSeason();

        // Act
        league.UpdateDetails("New Name", 15m, FutureDeadline, 5, 2, season, _dateTimeProvider);

        // Assert
        league.Name.Should().Be("New Name");
        league.Price.Should().Be(15m);
        league.EntryDeadlineUtc.Should().Be(FutureDeadline);
        league.PointsForExactScore.Should().Be(5);
        league.PointsForCorrectResult.Should().Be(2);
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeId_WhenUpdating()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var originalId = league.Id;

        // Act
        league.UpdateDetails("New Name", 15m, FutureDeadline, 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.Id.Should().Be(originalId);
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeSeasonId_WhenUpdating()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var originalSeasonId = league.SeasonId;

        // Act
        league.UpdateDetails("New Name", 15m, FutureDeadline, 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.SeasonId.Should().Be(originalSeasonId);
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeAdministratorUserId_WhenUpdating()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var originalAdminId = league.AdministratorUserId;

        // Act
        league.UpdateDetails("New Name", 15m, FutureDeadline, 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.AdministratorUserId.Should().Be(originalAdminId);
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeCreatedAtUtc_WhenUpdating()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var originalCreatedAt = league.CreatedAtUtc;

        // Act
        league.UpdateDetails("New Name", 15m, FutureDeadline, 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        league.CreatedAtUtc.Should().Be(originalCreatedAt);
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsNull()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.UpdateDetails(null!, 15m, FutureDeadline, 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsEmpty()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.UpdateDetails("", 15m, FutureDeadline, 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineIsInThePast()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.UpdateDetails("New Name", 15m, _dateTimeProvider.UtcNow.AddDays(-1), 5, 2, CreateFutureSeason(), _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDeadlineAfterSeasonStart()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var season = CreateFutureSeason();

        // Act
        var act = () => league.UpdateDetails("New Name", 15m, season.StartDateUtc.AddDays(1), 5, 2, season, _dateTimeProvider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region DefinePrizes

    [Fact]
    public void DefinePrizes_ShouldSetHasPrizesTrue_WhenPrizesProvided()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var prizes = new[] { LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 100m) };

        // Act
        league.DefinePrizes(prizes);

        // Assert
        league.HasPrizes.Should().BeTrue();
    }

    [Fact]
    public void DefinePrizes_ShouldSetHasPrizesFalse_WhenEmptyList()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        league.DefinePrizes([]);

        // Assert
        league.HasPrizes.Should().BeFalse();
    }

    [Fact]
    public void DefinePrizes_ShouldSetHasPrizesFalse_WhenNull()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        league.DefinePrizes(null);

        // Assert
        league.HasPrizes.Should().BeFalse();
    }

    [Fact]
    public void DefinePrizes_ShouldClearExistingPrizes_WhenCalledAgain()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        league.DefinePrizes([LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 100m)]);

        // Act
        var newPrizes = new[]
        {
            LeaguePrizeSetting.Create(1, PrizeType.Round, 1, 50m),
            LeaguePrizeSetting.Create(1, PrizeType.Round, 2, 25m)
        };
        league.DefinePrizes(newPrizes);

        // Assert — only the second set remains
        league.PrizeSettings.Should().HaveCount(2);
    }

    [Fact]
    public void DefinePrizes_ShouldPopulatePrizeSettingsCollection_WhenPrizesProvided()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        var prizes = new[]
        {
            LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 100m),
            LeaguePrizeSetting.Create(1, PrizeType.Overall, 2, 50m)
        };

        // Act
        league.DefinePrizes(prizes);

        // Assert
        league.PrizeSettings.Should().HaveCount(2);
    }

    [Fact]
    public void DefinePrizes_ShouldClearPrizeSettingsCollection_WhenCalledWithEmpty()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        league.DefinePrizes([LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 100m)]);

        // Act
        league.DefinePrizes([]);

        // Assert
        league.PrizeSettings.Should().BeEmpty();
    }

    #endregion

    #region SetPrizeFundOverride

    [Fact]
    public void SetPrizeFundOverride_ShouldSetAmount_WhenValueProvided()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        league.SetPrizeFundOverride(100m);

        // Assert
        league.PrizeFundOverride.Should().Be(100m);
    }

    [Fact]
    public void SetPrizeFundOverride_ShouldClearAmount_WhenNull()
    {
        // Arrange
        var league = CreateLeagueViaFactory();
        league.SetPrizeFundOverride(100m);

        // Act
        league.SetPrizeFundOverride(null);

        // Assert
        league.PrizeFundOverride.Should().BeNull();
    }

    #endregion

    #region ReassignAdministrator

    [Fact]
    public void ReassignAdministrator_ShouldUpdateAdministratorUserId()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        league.ReassignAdministrator("new-admin");

        // Assert
        league.AdministratorUserId.Should().Be("new-admin");
    }

    [Fact]
    public void ReassignAdministrator_ShouldThrowException_WhenUserIdIsNull()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.ReassignAdministrator(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReassignAdministrator_ShouldThrowException_WhenUserIdIsEmpty()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.ReassignAdministrator("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReassignAdministrator_ShouldThrowException_WhenUserIdIsWhitespace()
    {
        // Arrange
        var league = CreateLeagueViaFactory();

        // Act
        var act = () => league.ReassignAdministrator(" ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
