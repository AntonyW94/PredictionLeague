using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ThePredictions.API.Middleware;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Middleware;

/// <summary>
/// These headers are the browser-side half of the application's defences, and losing one is silent:
/// the site keeps working, so nothing but a test notices that framing, MIME sniffing or inline script
/// is allowed again. The Content-Security-Policy directives are asserted individually because each is
/// load-bearing for Blazor WASM - dropping <c>wasm-unsafe-eval</c> breaks the app, and widening
/// <c>script-src</c> to <c>unsafe-inline</c> would quietly undo the XSS protection the policy exists
/// to give.
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private static async Task<HttpContext> InvokeWith(bool isHttps)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = isHttps ? "https" : "http";

        var nextWasCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextWasCalled.Should().BeTrue("the middleware must always continue the pipeline");

        return context;
    }

    // ---------- headers applied to every response ----------

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("X-XSS-Protection", "1; mode=block")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    public async Task InvokeAsync_ShouldSetTheHeader_OnEveryResponse(string header, string expectedValue)
    {
        var context = await InvokeWith(isHttps: true);

        context.Response.Headers[header].ToString().Should().Be(expectedValue);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDenyTheBrowserFeaturesTheApplicationDoesNotUse()
    {
        var context = await InvokeWith(isHttps: true);

        var permissionsPolicy = context.Response.Headers["Permissions-Policy"].ToString();

        permissionsPolicy.Should().Contain("camera=()");
        permissionsPolicy.Should().Contain("geolocation=()");
        permissionsPolicy.Should().Contain("microphone=()");
        permissionsPolicy.Should().Contain("payment=()");
        permissionsPolicy.Should().Contain("usb=()");
    }

    // ---------- content security policy ----------

    [Theory]
    [InlineData("default-src 'self'")]
    [InlineData("script-src 'self' 'wasm-unsafe-eval'")]
    [InlineData("style-src 'self' 'unsafe-inline'")]
    [InlineData("img-src 'self' data: https:")]
    [InlineData("font-src 'self' data:")]
    [InlineData("connect-src 'self' https://accounts.google.com")]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("form-action 'self'")]
    [InlineData("base-uri 'self'")]
    [InlineData("upgrade-insecure-requests")]
    public async Task InvokeAsync_ShouldIncludeTheContentSecurityPolicyDirective(string directive)
    {
        var context = await InvokeWith(isHttps: true);

        context.Response.Headers["Content-Security-Policy"].ToString().Should().Contain(directive);
    }

    // Inline script is the one thing the policy must never allow; 'wasm-unsafe-eval' is narrower and
    // is what Blazor actually needs.
    [Fact]
    public async Task InvokeAsync_ShouldNotAllowInlineScript()
    {
        var context = await InvokeWith(isHttps: true);

        context.Response.Headers["Content-Security-Policy"].ToString()
            .Should().NotContain("script-src 'self' 'unsafe-inline'");
    }

    // ---------- HSTS is conditional on the scheme ----------

    [Fact]
    public async Task InvokeAsync_ShouldSendStrictTransportSecurity_WhenTheRequestIsHttps()
    {
        var context = await InvokeWith(isHttps: true);

        context.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    // Sending HSTS over plain HTTP would pin localhost to HTTPS for a year and make local development
    // unreachable, so the header is deliberately withheld.
    [Fact]
    public async Task InvokeAsync_ShouldNotSendStrictTransportSecurity_WhenTheRequestIsNotHttps()
    {
        var context = await InvokeWith(isHttps: false);

        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }
}
