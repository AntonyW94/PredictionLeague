using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using ThePredictions.API.Services;
using ThePredictions.Domain.Common.Constants;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Services;

/// <summary>
/// Handlers read the caller's identity through this, and EnsureAdministrator is the check standing
/// between an ordinary player and the admin endpoints.
/// </summary>
public class CurrentUserServiceTests
{
    private const string UserId = "user-1";

    private readonly IHttpContextAccessor _accessor = Substitute.For<IHttpContextAccessor>();

    private CurrentUserService BuildService(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            _accessor.HttpContext.Returns((HttpContext?)null);
            return new CurrentUserService(_accessor);
        }

        _accessor.HttpContext.Returns(new DefaultHttpContext { User = user });
        return new CurrentUserService(_accessor);
    }

    private static ClaimsPrincipal SignedIn(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, UserId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    /// <summary>A principal carrying no identity at all, as an unauthenticated request produces.</summary>
    private static ClaimsPrincipal WithoutIdentity() => new();

    [Fact]
    public void IsAuthenticated_ShouldBeFalse_WhenThePrincipalHasNoIdentity()
    {
        BuildService(WithoutIdentity()).IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void UserId_ShouldComeFromTheNameIdentifierClaim()
    {
        BuildService(SignedIn()).UserId.Should().Be(UserId);
    }

    [Fact]
    public void UserId_ShouldBeNull_WhenThereIsNoHttpContext()
    {
        BuildService(null).UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_ShouldBeNull_WhenTheCallerIsAnonymous()
    {
        BuildService(Anonymous()).UserId.Should().BeNull();
    }

    [Fact]
    public void IsAuthenticated_ShouldBeTrue_ForASignedInCaller()
    {
        BuildService(SignedIn()).IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_ShouldBeFalse_ForAnAnonymousCaller()
    {
        BuildService(Anonymous()).IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_ShouldBeFalse_WhenThereIsNoHttpContext()
    {
        BuildService(null).IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsAdministrator_ShouldBeTrue_ForAnAdministrator()
    {
        BuildService(SignedIn(RoleNames.Administrator)).IsAdministrator.Should().BeTrue();
    }

    [Fact]
    public void IsAdministrator_ShouldBeFalse_ForAnOrdinaryPlayer()
    {
        BuildService(SignedIn()).IsAdministrator.Should().BeFalse();
    }

    [Fact]
    public void IsAdministrator_ShouldBeFalse_WhenThereIsNoHttpContext()
    {
        BuildService(null).IsAdministrator.Should().BeFalse();
    }

    [Fact]
    public void EnsureAdministrator_ShouldPass_ForAnAdministrator()
    {
        var act = () => BuildService(SignedIn(RoleNames.Administrator)).EnsureAdministrator();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureAdministrator_ShouldRejectAnAnonymousCaller_AsUnauthenticated()
    {
        var act = () => BuildService(Anonymous()).EnsureAdministrator();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Authentication is required to access this resource.");
    }

    [Fact]
    public void EnsureAdministrator_ShouldRejectASignedInNonAdministrator()
    {
        var act = () => BuildService(SignedIn()).EnsureAdministrator();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Administrator privileges are required to access this resource.");
    }
}
