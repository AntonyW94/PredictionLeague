using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ThePredictions.API.Controllers;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Controllers;

/// <summary>
/// Exercises the open-redirect guard through the public Google sign-in action. The action stashes
/// the sanitised values in the challenge's authentication properties, so asserting on those tests
/// the real entry point rather than reaching into a private helper.
/// </summary>
public class ExternalAuthControllerRedirectSafetyTests
{
    private const string SiteHost = "www.thepredictions.co.uk";

    private static ExternalAuthController BuildController(string host = SiteHost)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:RefreshTokenExpiryDays"] = "30" })
            .Build();

        var controller = new ExternalAuthController(
            NullLogger<ExternalAuthController>.Instance,
            Substitute.For<IMediator>(),
            configuration);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>()).Returns("/external-auth/signin-google");
        controller.Url = urlHelper;

        return controller;
    }

    private static (string ReturnUrl, string Source) Sanitise(string? returnUrl, string? source, string host = SiteHost)
    {
        var controller = BuildController(host);

        var result = controller.GoogleLogin(returnUrl!, source!);

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties.Should().NotBeNull();

        return (challenge.Properties!.Items["returnUrl"]!, challenge.Properties.Items["source"]!);
    }

    [Fact]
    public void GoogleLogin_ShouldKeepASimpleLocalPath()
    {
        var (returnUrl, _) = Sanitise("/dashboard", "/login");

        returnUrl.Should().Be("/dashboard");
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/leagues/7")]
    [InlineData("/account/details")]
    public void GoogleLogin_ShouldKeepALocalPath_OnEveryPlatform(string path)
    {
        // Regression guard. On Linux, Uri.TryCreate parses "/dashboard" as the absolute file URI
        // "file:///dashboard"; its empty host then failed the same-host check and the return URL
        // was thrown away, sending every Google sign-in to "/" once deployed. Windows parses the
        // same string as relative, so this only ever failed off a developer machine.
        var (returnUrl, _) = Sanitise(path, "/login");

        returnUrl.Should().Be(path);
    }

    [Fact]
    public void GoogleLogin_ShouldKeepAPathWithAQueryString()
    {
        var (returnUrl, _) = Sanitise("/leagues?id=7&tab=table", "/login");

        returnUrl.Should().Be("/leagues?id=7&tab=table");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GoogleLogin_ShouldFallBack_WhenNoUrlIsSupplied(string? url)
    {
        var (returnUrl, source) = Sanitise(url, url);

        returnUrl.Should().Be("/");
        source.Should().Be("/login");
    }

    [Fact]
    public void GoogleLogin_ShouldPrependASlash_ToABarePath()
    {
        var (returnUrl, _) = Sanitise("dashboard", "/login");

        returnUrl.Should().Be("/dashboard");
    }

    [Fact]
    public void GoogleLogin_ShouldReduceAnAbsoluteUrlOnOurOwnHostToItsPath()
    {
        var (returnUrl, _) = Sanitise("https://www.thepredictions.co.uk/dashboard?tab=1", "/login");

        returnUrl.Should().Be("/dashboard?tab=1");
    }

    [Fact]
    public void GoogleLogin_ShouldTreatWwwAndNonWwwAsTheSameHost()
    {
        var (returnUrl, _) = Sanitise("https://thepredictions.co.uk/dashboard", "/login", host: "www.thepredictions.co.uk");

        returnUrl.Should().Be("/dashboard");
    }

    [Fact]
    public void GoogleLogin_ShouldTreatNonWwwRequestAgainstWwwUrlAsTheSameHost()
    {
        var (returnUrl, _) = Sanitise("https://www.thepredictions.co.uk/dashboard", "/login", host: "thepredictions.co.uk");

        returnUrl.Should().Be("/dashboard");
    }

    [Fact]
    public void GoogleLogin_ShouldRejectAnAbsoluteUrlPointingAtAnotherSite()
    {
        var (returnUrl, _) = Sanitise("https://evil.example.com/steal", "/login");

        returnUrl.Should().Be("/");
    }

    [Fact]
    public void GoogleLogin_ShouldRejectAProtocolRelativeUrl()
    {
        // "//evil.com" is a protocol-relative URL: the browser treats it as an absolute address.
        var (returnUrl, _) = Sanitise("//evil.example.com", "/login");

        returnUrl.Should().Be("/");
    }

    [Fact]
    public void GoogleLogin_ShouldRejectAPathContainingABackslash()
    {
        // Some browsers normalise "/\evil.com" into a protocol-relative URL.
        var (returnUrl, _) = Sanitise(@"/\evil.example.com", "/login");

        returnUrl.Should().Be("/");
    }

    [Fact]
    public void GoogleLogin_ShouldRejectABareHostThatResolvesToAnotherSite()
    {
        var (returnUrl, _) = Sanitise("https://evil.example.com", "/login");

        returnUrl.Should().Be("/");
    }

    [Fact]
    public void GoogleLogin_ShouldUseTheSourceSpecificFallback_WhenTheSourceIsRejected()
    {
        var (returnUrl, source) = Sanitise("https://evil.example.com", "https://evil.example.com/login");

        returnUrl.Should().Be("/");
        source.Should().Be("/login");
    }

    [Fact]
    public void GoogleLogin_ShouldSanitiseTheSourceIndependentlyOfTheReturnUrl()
    {
        var (returnUrl, source) = Sanitise("/dashboard", "register");

        returnUrl.Should().Be("/dashboard");
        source.Should().Be("/register");
    }

    [Fact]
    public void GoogleLogin_ShouldChallengeTheGoogleScheme()
    {
        var controller = BuildController();

        var result = controller.GoogleLogin("/dashboard", "/login");

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.AuthenticationSchemes.Should().ContainSingle().Which.Should().Be("Google");
        challenge.Properties!.RedirectUri.Should().Be("/external-auth/signin-google");
    }
}
