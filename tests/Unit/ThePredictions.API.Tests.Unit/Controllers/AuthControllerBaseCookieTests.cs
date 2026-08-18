using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ThePredictions.API.Controllers;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Controllers;

/// <summary>
/// The refresh-token cookie has to be written with the right domain, Secure and SameSite settings or
/// sign-in silently breaks: too strict and localhost cannot set it over plain HTTP, too loose and dev
/// and prod share one session. These assert the Set-Cookie headers the base controller actually emits.
/// </summary>
/// <remarks>
/// The cookies are host-only. They were scoped to ".thepredictions.co.uk" until August 2026, which meant
/// dev and prod shared a single refresh token and signing into either logged you out of the other. Every
/// write now also expires that old domain-scoped pair, so the tests below check for two cookies per name:
/// the live host-only one, and the legacy expiry clearing up after it.
/// </remarks>
public class AuthControllerBaseCookieTests
{
    private const int RefreshTokenExpiryDays = 30;
    private const string LegacyDomain = "domain=.thepredictions.co.uk";

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

    private static string[] SetCookies(TestAuthController controller) =>
        controller.Response.Headers.SetCookie.ToArray()!;

    private static string JoinedHeader(TestAuthController controller) =>
        string.Join(" | ", SetCookies(controller));

    /// <summary>The one Set-Cookie actually carrying a value, as opposed to a legacy expiry clearing up.</summary>
    private static string LiveCookie(TestAuthController controller, string name) =>
        SetCookies(controller).Single(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal) && !cookie.Contains(LegacyDomain));

    private static string[] LegacyCookies(TestAuthController controller) =>
        SetCookies(controller).Where(cookie => cookie.Contains(LegacyDomain)).ToArray();

    // ---------- cookie options on a real host ----------

    [Fact]
    public void SetTokenCookie_ShouldWriteAHostOnlyCookie_OnARealHost()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        // No domain: this is what stops dev and prod sharing a session.
        var cookie = LiveCookie(controller, "refreshToken");
        cookie.Should().NotContain("domain=");
        cookie.Should().Contain("secure");
        cookie.Should().Contain("samesite=none");
        cookie.Should().Contain("httponly");
        cookie.Should().Contain("path=/");
    }

    [Fact]
    public void SetTokenCookie_ShouldWriteBothTheTokenAndTheRememberMeFlag()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        LiveCookie(controller, "refreshToken").Should().StartWith("refreshToken=token-value");
        LiveCookie(controller, "rememberMe").Should().StartWith("rememberMe=1");
    }

    [Fact]
    public void SetTokenCookie_ShouldWriteAZeroFlag_WhenTheSessionIsNotPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: false);

        LiveCookie(controller, "rememberMe").Should().StartWith("rememberMe=0");
    }

    [Fact]
    public void SetTokenCookie_ShouldDateTheCookie_WhenTheSessionIsPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        LiveCookie(controller, "refreshToken").Should().Contain("expires=");
    }

    [Fact]
    public void SetTokenCookie_ShouldLeaveTheCookieSessionScoped_WhenTheSessionIsNotPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: false);

        LiveCookie(controller, "refreshToken").Should().NotContain("expires=");
    }

    // ---------- clearing up the old domain-scoped pair ----------

    [Fact]
    public void SetTokenCookie_ShouldExpireTheLegacyDomainScopedPair()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        var legacy = LegacyCookies(controller);
        legacy.Should().HaveCount(2);
        legacy.Should().OnlyContain(cookie => cookie.Contains("expires=Thu, 01 Jan 1970"));
        legacy.Should().Contain(cookie => cookie.StartsWith("refreshToken=", StringComparison.Ordinal));
        legacy.Should().Contain(cookie => cookie.StartsWith("rememberMe=", StringComparison.Ordinal));
    }

    [Fact]
    public void SetTokenCookie_ShouldKeepTheNewCookies_WhenItExpiresTheLegacyPair()
    {
        // Response.Cookies.Delete drops pending Set-Cookie headers of the same name, and the legacy delete
        // runs one line after the write. It survives only because the domain differs - so if that ever
        // stops being true, this is what catches it rather than a wave of logged-out players.
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetTokenCookie("token-value", persistent: true);

        LiveCookie(controller, "refreshToken").Should().StartWith("refreshToken=token-value");
        SetCookies(controller).Should().HaveCount(4);
    }

    [Fact]
    public void SetTokenCookie_ShouldNotWriteALegacyExpiry_OnLocalhost()
    {
        // Localhost never had a domain cookie to clear, so nothing to undo there.
        var controller = BuildController("localhost");

        controller.CallSetTokenCookie("token-value", persistent: true);

        LegacyCookies(controller).Should().BeEmpty();
        SetCookies(controller).Should().HaveCount(2);
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

        var header = JoinedHeader(controller);
        header.Should().NotContain("domain=");
        header.Should().Contain("samesite=lax");
        header.Should().NotContain("secure");
    }

    [Fact]
    public void SetTokenCookie_ShouldStillMarkTheCookieSecure_WhenLocalhostIsServedOverHttps()
    {
        var controller = BuildController("localhost");

        controller.CallSetTokenCookie("token-value", persistent: true);

        var header = JoinedHeader(controller);
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

        LiveCookie(controller, "rememberMe").Should().StartWith("rememberMe=1").And.Contain("expires=");

        // No live refresh token - only the legacy expiry clearing up the old one.
        SetCookies(controller).Should().NotContain(cookie => cookie.StartsWith("refreshToken=", StringComparison.Ordinal) && !cookie.Contains(LegacyDomain));
    }

    [Fact]
    public void SetRememberMePreference_ShouldLeaveTheFlagSessionScoped_WhenNotPersistent()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetRememberMePreference(persistent: false);

        LiveCookie(controller, "rememberMe").Should().StartWith("rememberMe=0").And.NotContain("expires=");
    }

    [Fact]
    public void SetRememberMePreference_ShouldAlsoExpireTheLegacyPair()
    {
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallSetRememberMePreference(persistent: true);

        LegacyCookies(controller).Should().HaveCount(2);
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

        // A cookie can only be cleared by a Set-Cookie whose attributes match the original, so the
        // host-only pair is cleared host-only.
        var cookie = LiveCookie(controller, "refreshToken");
        cookie.Should().NotContain("domain=");
        cookie.Should().Contain("expires=Thu, 01 Jan 1970");
        LiveCookie(controller, "rememberMe").Should().Contain("expires=Thu, 01 Jan 1970");
    }

    [Fact]
    public void DeleteTokenCookie_ShouldAlsoExpireTheLegacyDomainScopedPair()
    {
        // Signing out has to clear the old shared cookie too, or the browser keeps presenting it.
        var controller = BuildController("www.thepredictions.co.uk");

        controller.CallDeleteTokenCookie();

        LegacyCookies(controller).Should().HaveCount(2);
        SetCookies(controller).Should().HaveCount(4);
    }
}
