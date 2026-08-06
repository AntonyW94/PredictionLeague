using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ThePredictions.API.Controllers;
using ThePredictions.Application.Features.Authentication.Commands.LoginWithGoogle;
using ThePredictions.Contracts.Authentication;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Controllers;

/// <summary>
/// Where Google hands the user back to us. It has to land them on the page they started from, and
/// on failure send them somewhere sensible with the reason attached rather than a blank screen.
/// </summary>
public class ExternalAuthControllerCallbackTests
{
    private const string SiteHost = "www.thepredictions.co.uk";

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();

    private record UnknownAuthenticationResponse() : AuthenticationResponse(false);

    private ExternalAuthController BuildController(string returnUrl = "/dashboard", string source = "/login")
    {
        var properties = new AuthenticationProperties();
        properties.Items["returnUrl"] = returnUrl;
        properties.Items["source"] = source;

        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()), properties, "Identity.External");
        _authenticationService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>())
            .Returns(AuthenticateResult.Success(ticket));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:RefreshTokenExpiryDays"] = "30" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(_authenticationService);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        httpContext.Request.Host = new HostString(SiteHost);

        return new ExternalAuthController(NullLogger<ExternalAuthController>.Instance, _mediator, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private void GivenLoginResult(AuthenticationResponse response) =>
        _mediator.Send(Arg.Any<LoginWithGoogleCommand>(), Arg.Any<CancellationToken>()).Returns(response);

    [Fact]
    public async Task GoogleCallbackAsync_ShouldSendTheUserBackWithTheirToken_OnSuccess()
    {
        GivenLoginResult(new SuccessfulAuthenticationResponse("access", DateTime.UtcNow, "refresh-token-value"));
        var controller = BuildController();

        var result = await controller.GoogleCallbackAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/dashboard?refreshToken=refresh-token-value&source=/login");
    }

    [Fact]
    public async Task GoogleCallbackAsync_ShouldUrlEncodeTheToken()
    {
        GivenLoginResult(new SuccessfulAuthenticationResponse("access", DateTime.UtcNow, "a+b/c=="));
        var controller = BuildController();

        var result = await controller.GoogleCallbackAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Contain("refreshToken=a%2Bb%2Fc%3D%3D");
    }

    [Fact]
    public async Task GoogleCallbackAsync_ShouldMarkTheSessionPersistent_OnSuccess()
    {
        // Google sign-ins are always remembered, so the follow-up token exchange writes a
        // persistent cookie regardless of any earlier email-login preference.
        GivenLoginResult(new SuccessfulAuthenticationResponse("access", DateTime.UtcNow, "refresh"));
        var controller = BuildController();

        await controller.GoogleCallbackAsync(CancellationToken.None);

        string.Join(" ", controller.Response.Headers.SetCookie.ToArray()).Should().Contain("rememberMe=1");
    }

    [Fact]
    public async Task GoogleCallbackAsync_ShouldRedirectWithTheReason_WhenTheLoginFails()
    {
        GivenLoginResult(new ExternalLoginFailedAuthenticationResponse("That account is blocked.", "/login"));
        var controller = BuildController();

        var result = await controller.GoogleCallbackAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("/login?error=That%20account%20is%20blocked.");
    }

    [Fact]
    public async Task GoogleCallbackAsync_ShouldNotTrustTheFailureSource()
    {
        GivenLoginResult(new ExternalLoginFailedAuthenticationResponse("Nope.", "https://evil.example.com"));
        var controller = BuildController();

        var result = await controller.GoogleCallbackAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().StartWith("/login?error=");
    }

    [Fact]
    public async Task GoogleCallbackAsync_ShouldRedirectWithAGenericMessage_ForAnUnrecognisedResult()
    {
        GivenLoginResult(new UnknownAuthenticationResponse());
        var controller = BuildController();

        var result = await controller.GoogleCallbackAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("/login?error=An%20unknown%20authentication%20error%20occurred.");
    }

    [Fact]
    public async Task GoogleCallbackAsync_ShouldFallBackToSafeDefaults_WhenGoogleReturnsAnExternalUrl()
    {
        GivenLoginResult(new SuccessfulAuthenticationResponse("access", DateTime.UtcNow, "refresh"));
        var controller = BuildController(returnUrl: "https://evil.example.com", source: "https://evil.example.com/login");

        var result = await controller.GoogleCallbackAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().StartWith("/?refreshToken=").And.EndWith("&source=/login");
    }
}
