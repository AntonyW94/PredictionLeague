using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.API.Controllers;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Controllers;

/// <summary>
/// Every authenticated action reads the caller's identity through these three properties, so they are
/// the single point where a missing claim is caught. <c>CurrentUserId</c> throwing rather than
/// returning null is what stops a token without a subject claim being treated as some other user's
/// request; the two name properties deliberately do the opposite and fall back to empty, because a
/// display name is cosmetic and must never fail a request.
/// </summary>
public class ApiControllerBaseClaimsTests
{
    /// <summary>Concrete stand-in: the base class is abstract and the properties are protected.</summary>
    private sealed class TestApiController : ApiControllerBase
    {
        public string CallCurrentUserId => CurrentUserId;
        public string CallCurrentUserFirstName => CurrentUserFirstName;
        public string CallCurrentUserLastName => CurrentUserLastName;
    }

    private static TestApiController BuildController(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
        };

        return new TestApiController
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    // ---------- current user id ----------

    [Fact]
    public void CurrentUserId_ShouldReturnTheSubjectClaim_WhenThePrincipalIsAuthenticated()
    {
        var controller = BuildController(new Claim(ClaimTypes.NameIdentifier, "user-42"));

        controller.CallCurrentUserId.Should().Be("user-42");
    }

    // The guard that matters: without it a token missing its subject claim would flow a null user id
    // into a handler, where it would silently match no rows instead of being rejected.
    [Fact]
    public void CurrentUserId_ShouldThrowUnauthorised_WhenTheSubjectClaimIsMissing()
    {
        var controller = BuildController(new Claim("FirstName", "Ada"));

        var act = () => controller.CallCurrentUserId;

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("User ID could not be found in the token.");
    }

    // ---------- display name claims ----------

    [Fact]
    public void CurrentUserFirstName_ShouldReturnTheClaim_WhenPresent()
    {
        var controller = BuildController(new Claim("FirstName", "Ada"));

        controller.CallCurrentUserFirstName.Should().Be("Ada");
    }

    [Fact]
    public void CurrentUserLastName_ShouldReturnTheClaim_WhenPresent()
    {
        var controller = BuildController(new Claim("LastName", "Lovelace"));

        controller.CallCurrentUserLastName.Should().Be("Lovelace");
    }

    // A cosmetic claim must never fail the request, so these fall back rather than throw.
    [Fact]
    public void CurrentUserFirstName_ShouldFallBackToEmpty_WhenTheClaimIsMissing()
    {
        var controller = BuildController(new Claim(ClaimTypes.NameIdentifier, "user-42"));

        controller.CallCurrentUserFirstName.Should().BeEmpty();
    }

    [Fact]
    public void CurrentUserLastName_ShouldFallBackToEmpty_WhenTheClaimIsMissing()
    {
        var controller = BuildController(new Claim(ClaimTypes.NameIdentifier, "user-42"));

        controller.CallCurrentUserLastName.Should().BeEmpty();
    }
}
