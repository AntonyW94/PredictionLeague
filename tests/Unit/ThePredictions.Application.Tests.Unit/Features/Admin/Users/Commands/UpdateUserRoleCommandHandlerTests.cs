using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Admin.Users.Commands;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Commands;

/// <summary>
/// Changing what an account is allowed to do. A role swap has to replace what was there rather than
/// add to it, or someone demoted would quietly keep their old permissions.
/// </summary>
public class UpdateUserRoleCommandHandlerTests
{
    private const string UserId = "user-1";

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly UpdateUserRoleCommandHandler _handler;

    public UpdateUserRoleCommandHandlerTests()
    {
        _handler = new UpdateUserRoleCommandHandler(_userManager, _currentUser);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(UserManagerResult.Success());
        _userManager.RemoveFromRolesAsync(Arg.Any<ApplicationUser>(), Arg.Any<IEnumerable<string>>()).Returns(UserManagerResult.Success());
    }

    private ApplicationUser GivenUser(params string[] currentRoles)
    {
        var user = new ApplicationUser { Id = UserId, Email = "alice@example.com", FirstName = "Alice", LastName = "Anderson" };
        _userManager.FindByIdAsync(UserId).Returns(user);
        _userManager.GetRolesAsync(user).Returns(currentRoles.ToList());
        return user;
    }

    private Task HandleAsync(string newRole = "Administrator") =>
        _handler.Handle(new UpdateUserRoleCommand(UserId, newRole), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        GivenUser();

        await HandleAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheAccountDoesNotExist()
    {
        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldClearTheOldRolesBeforeGrantingTheNewOne()
    {
        // Granting without clearing would leave a demoted admin still holding admin rights.
        var user = GivenUser("Administrator");

        await HandleAsync("User");

        Received.InOrder(() =>
        {
            _userManager.RemoveFromRolesAsync(user, Arg.Is<IEnumerable<string>>(r => r.Contains("Administrator")));
            _userManager.AddToRoleAsync(user, "User");
        });
    }

    [Fact]
    public async Task Handle_ShouldGrantTheRole_WhenTheAccountHadNoneBefore()
    {
        var user = GivenUser();

        await HandleAsync("Administrator");

        await _userManager.Received(1).AddToRoleAsync(user, "Administrator");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheRoleCannotBeGranted()
    {
        GivenUser();
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(UserManagerResult.Failure(["Role does not exist", "Something else"]));

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<Exception>())
            .WithMessage("Failed to update role: Role does not exist, Something else");
    }
}
