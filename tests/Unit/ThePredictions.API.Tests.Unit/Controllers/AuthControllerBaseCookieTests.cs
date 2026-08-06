using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ThePredictions.API.Controllers;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Controllers;

/// <summary>
/// The refresh-token cookie has to be written with the right domain, Secure and SameSite settings
/// or sign-in silently breaks: too strict and localhost cannot set it over plain HTTP, too loose
/// and it stops being shared across the www/dev subdomains. These assert the Set-Cookie header the
/// base controller actually emits.
/// </summary>
public class AuthControllerBaseCookieTests
{
    private const int RefreshTokenExpiryDays = 30;

    /// <summary>Concrete stand-in: the base class is abstract and its cookie API is protected.</summary>
    private sealed class TestAuthController(IConfiguration configuration) : AuthControllerBase(configuration)
    {
        public void CallSetTokenCookie(string token, bool persistent) => SetTokenCookie(token, persistent);
        public void CallSetRememberMePreference(bool persistent) => SetRememberMePreference(persistent);
        public void CallDeleteTokenCookie() => DeleteTokenCookie();
        public bool CallShouldPersistSession() => ShouldPersistSession();
    }

    private sealed class ThrowingCookiesFeature : IResponseCookiesFeature
    {
        public IResponseCookies Cookies { get; } = new ThrowingCookies();

        private sealed class ThrowingCookies : IResponseCookies
        {
            public void Append(string key, string value) => throw new InvalidOperationException("no");
            public void Append(string key, string value, CookieOptions options) => throw new InvalidOperationException("no");
            public void Delete(string key) => throw new InvalidOperationException("no");
            public void Delete(string key, CookieOptions options) => throw new InvalidOperationException("no");
        }
    }

    private static TestAuthController BuildController(string host, bool isHttps = true, HttpContext? httpContext = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:RefreshTokenExpiryDays"] = RefreshTokenExpiryDays.ToString()
            })
            .Build();

        var context = httpContext ?? new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Scheme = isHttps ? "https" : "http";

        return new TestAuthController(configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static string SetCookieHeader(TestAuthController controller) =>
        string.Join(" | ", controller.Response.Headers.SetCookie.ToArray());

    // ---------- cookie options on a real host ----------

    [Fact]
    public void SetTokenCookie_ShouldShareTheCookieAcrossSubdomains_OnARealHost()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        var header = SetCookieHeader(controller);
        header.Should().Contain("domain=.thepredictions.co.uk");
        header.Should().Contain("secure");
        header.Should().Contain("samesite=none");
        header.Should().Contain("httponly");
        header.Should().Contain("path=/");
    }

    [Fact]
    public void SetTokenCookie_ShouldWriteBothTheTokenAndTheRememberMeFlag()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        var header = SetCookieHeader(controller);
        header.Should().Contain("refreshToken=token-value");
        header.Should().Contain("rememberMe=1");
    }

    [Fact]
    public void SetTokenCookie_ShouldWriteAZeroFlag_WhenTheSessionIsNotPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: false);

        SetCookieHeader(controller).Should().Contain("rememberMe=0");
    }

    [Fact]
    public void SetTokenCookie_ShouldDateTheCookie_WhenTheSessionIsPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        SetCookieHeader(controller).Should().Contain("expires=");
    }

    [Fact]
    public void SetTokenCookie_ShouldLeaveTheCookieSessionScoped_WhenTheSessionIsNotPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: false);

        SetCookieHeader(controller).Should().NotContain("expires=");
    }

    // ---------- localhost fallback ----------

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void SetTokenCookie_ShouldWriteAHostOnlyLaxCookie_OnLocalhost(string host)
    {
        var controller = BuildController(host, isHttps: false);

        controller.CallSetTokenCookie("token-value", persistent: true);

        var header = SetCookieHeader(controller);
        header.Should().NotContain("domain=");
        header.Should().Contain("samesite=lax");
        header.Should().NotContain("secure");
    }

    [Fact]
    public void SetTokenCookie_ShouldStillMarkTheCookieSecure_WhenLocalhostIsServedOverHttps()
    {
        var controller = BuildController("localhost");

        controller.CallSetTokenCookie("token-value", persistent: true);

        var header = SetCookieHeader(controller);
        header.Should().Contain("secure");
        header.Should().Contain("samesite=lax");
    }

    // ---------- failure handling ----------

    [Fact]
    public void SetTokenCookie_ShouldReportAClearFailure_WhenTheCookieCannotBeWritten()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IResponseCookiesFeature>(new ThrowingCookiesFeature());
        var controller = BuildController("www.thepredictions.co.uk", httpContext: context);

        var act = () => controller.CallSetTokenCookie("token-value", persistent: true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed to set 'refreshToken' cookie.")
            .WithInnerException<InvalidOperationException>();
    }

    // ---------- remember-me preference ----------

    [Fact]
    public void SetRememberMePreference_ShouldWriteOnlyTheFlagCookie()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetRememberMePreference(persistent: true);

        var header = SetCookieHeader(controller);
        header.Should().Contain("rememberMe=1");
        header.Should().NotContain("refreshToken=");
        header.Should().Contain("expires=");
    }

    [Fact]
    public void SetRememberMePreference_ShouldLeaveTheFlagSessionScoped_WhenNotPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetRememberMePreference(persistent: false);

        var header = SetCookieHeader(controller);
        header.Should().Contain("rememberMe=0");
        header.Should().NotContain("expires=");
    }

    // ---------- reading the preference back ----------

    [Fact]
    public void ShouldPersistSession_ShouldBeFalse_WhenTheFlagSaysSessionOnly()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "rememberMe=0";
        var controller = BuildController("www.thepredictions.co.uk", httpContext: context);

        controller.CallShouldPersistSession().Should().BeFalse();
    }

    [Fact]
    public void ShouldPersistSession_ShouldBeTrue_WhenTheFlagSaysRemember()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "rememberMe=1";
        var controller = BuildController("www.thepredictions.co.uk", httpContext: context);

        controller.CallShouldPersistSession().Should().BeTrue();
    }

    [Fact]
    public void ShouldPersistSession_ShouldDefaultToTrue_ForASessionPredatingTheFlag()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallShouldPersistSession().Should().BeTrue();
    }

    // ---------- deletion ----------

    [Fact]
    public void DeleteTokenCookie_ShouldExpireBothCookiesWithMatchingOptions()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallDeleteTokenCookie();

        var header = SetCookieHeader(controller);
        header.Should().Contain("refreshToken=");
        header.Should().Contain("rememberMe=");
        // A cookie can only be cleared by a Set-Cookie whose attributes match the original.
        header.Should().Contain("domain=.thepredictions.co.uk");
        header.Should().Contain("expires=Thu, 01 Jan 1970");
    }
}
