using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Admin.Users.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Commands;

/// <summary>
/// Deleting an account from the admin screen. A league can never be left without an administrator,
/// so any league this person runs has to be handed to someone else in the same operation.
/// </summary>
public class DeleteUserCommandHandlerTests
{
    private const string UserIdToDelete = "user-1";
    private const string DeletingUserId = "admin-1";
    private const string NewAdministratorId = "user-2";

    private static readonly DateTime CreatedAtUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    private readonly DeleteUserCommandHandler _handler;

    public DeleteUserCommandHandlerTests()
    {
        _handler = new DeleteUserCommandHandler(_userManager, _leagueRepository, _currentUserService);
        _leagueRepository.GetLeaguesByAdministratorIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _userManager.DeleteAsync(Arg.Any<ApplicationUser>()).Returns(UserManagerResult.Success());
    }

    private static ApplicationUser User(string id) =>
        new() { Id = id, Email = $"{id}@example.com", FirstName = "Alice", LastName = "Anderson" };

    private ApplicationUser GivenUserExists(string id)
    {
        var user = User(id);
        _userManager.FindByIdAsync(id).Returns(user);
        return user;
    }

    private static League League(int id, string administratorId) =>
        new(id: id, name: $"League {id}", seasonId: 11, administratorUserId: administratorId,
            entryCode: $"CODE{id}", createdAtUtc: CreatedAtUtc, entryDeadlineUtc: CreatedAtUtc.AddDays(30),
            pointsForExactScore: 3, pointsForCorrectResult: 1, price: 0m, isFree: true,
            hasPrizes: false, prizeFundOverride: null, members: [], prizeSettings: []);

    private void GivenAdministeredLeagues(params League[] leagues) =>
        _leagueRepository.GetLeaguesByAdministratorIdAsync(UserIdToDelete, Arg.Any<CancellationToken>()).Returns(leagues);

    private Task HandleAsync(string? newAdministratorId = null, string userIdToDelete = UserIdToDelete) =>
        _handler.Handle(new DeleteUserCommand(userIdToDelete, DeletingUserId, newAdministratorId), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        GivenUserExists(UserIdToDelete);

        await HandleAsync();

        _currentUserService.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldRefuseToDeleteTheAdministratorsOwnAccount()
    {
        var act = () => HandleAsync(userIdToDelete: DeletingUserId);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*cannot delete their own account*");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheAccountDoesNotExist()
    {
        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldDeleteAnAccountThatRunsNoLeagues()
    {
        var user = GivenUserExists(UserIdToDelete);

        await HandleAsync();

        await _userManager.Received(1).DeleteAsync(user);
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRefuseToDelete_WhenTheyRunALeagueAndNoReplacementWasChosen()
    {
        GivenUserExists(UserIdToDelete);
        GivenAdministeredLeagues(League(1, UserIdToDelete));

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*must select a new administrator*");
        await _userManager.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldRefuseToDelete_WhenTheReplacementIsBlank()
    {
        GivenUserExists(UserIdToDelete);
        GivenAdministeredLeagues(League(1, UserIdToDelete));

        var act = () => HandleAsync(newAdministratorId: "   ");

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheChosenReplacementDoesNotExist()
    {
        GivenUserExists(UserIdToDelete);
        GivenAdministeredLeagues(League(1, UserIdToDelete));

        var act = () => HandleAsync(NewAdministratorId);

        await act.Should().ThrowAsync<Ardalis.GuardClauses.NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldHandEveryLeagueToTheReplacementBeforeDeleting()
    {
        var user = GivenUserExists(UserIdToDelete);
        GivenUserExists(NewAdministratorId);
        var first = League(1, UserIdToDelete);
        var second = League(2, UserIdToDelete);
        GivenAdministeredLeagues(first, second);

        await HandleAsync(NewAdministratorId);

        first.AdministratorUserId.Should().Be(NewAdministratorId);
        second.AdministratorUserId.Should().Be(NewAdministratorId);
        await _leagueRepository.Received(1).UpdateAsync(first, Arg.Any<CancellationToken>());
        await _leagueRepository.Received(1).UpdateAsync(second, Arg.Any<CancellationToken>());
        await _userManager.Received(1).DeleteAsync(user);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheAccountCannotBeDeleted()
    {
        GivenUserExists(UserIdToDelete);
        _userManager.DeleteAsync(Arg.Any<ApplicationUser>())
            .Returns(UserManagerResult.Failure(["Concurrency failure", "Something else"]));

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<Exception>())
            .WithMessage("Failed to delete user: Concurrency failure, Something else");
    }
}
