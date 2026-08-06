using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Web.Client.Authentication;
using ThePredictions.Web.Client.Tests.Unit.TestDoubles;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Authentication;

public class ApiAuthenticationStateProviderTests
{
    private const string AccessTokenKey = "accessToken";

    private readonly InMemoryLocalStorage _localStorage = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly TestNavigationManager _navigation = new();
    private readonly HttpClient _httpClient;

    public ApiAuthenticationStateProviderTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://localhost/") };
    }

    private ApiAuthenticationStateProvider CreateProvider() =>
        new(_httpClient, _localStorage, Substitute.For<ILogger<ApiAuthenticationStateProvider>>(), _navigation);

    private Task SeedTokenAsync(string token) =>
        _localStorage.SetItemAsync(AccessTokenKey, token, CancellationToken.None).AsTask();

    private Task<string?> StoredTokenAsync() =>
        _localStorage.GetItemAsync<string>(AccessTokenKey, CancellationToken.None).AsTask();

    private static SuccessfulAuthenticationResponse TokenResponse(string accessToken) =>
        new(accessToken, DateTime.UtcNow.AddMinutes(15), "refresh-token");

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAuthenticatedUser_WhenStoredTokenIsValid()
    {
        await SeedTokenAsync(TestJwt.Valid(new Claim(ClaimTypes.NameIdentifier, "user-1")));
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("user-1");
        _handler.SendCount.Should().Be(0, "a valid token must not trigger a network refresh");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldRefreshAndAuthenticate_WhenStoredTokenIsExpired()
    {
        await SeedTokenAsync(TestJwt.Expired());
        var newToken = TestJwt.Valid(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        _handler.EnqueueJson(HttpStatusCode.OK, TokenResponse(newToken));
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        (await StoredTokenAsync()).Should().Be(newToken);
        _handler.SendCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymous_WhenNoTokenAndRefreshFails()
    {
        _handler.EnqueueStatus(HttpStatusCode.Unauthorized);
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    // Regression test for the infinite auth-state loop that caused the login page
    // to flicker and hammer the rate-limited refresh endpoint: a failed refresh
    // must result in exactly ONE refresh attempt, not an unbounded re-entrant loop.
    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldAttemptRefreshOnlyOnce_WhenUnauthenticated()
    {
        _handler.FallbackStatus = HttpStatusCode.Unauthorized;
        var provider = CreateProvider();

        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();

        _handler.SendCount.Should().Be(1, "the anonymous result is cached and must not re-trigger refreshes");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousWithoutRefresh_OnExternalLoginCallback()
    {
        var navigation = new TestNavigationManager("https://localhost/authentication/external-login-callback?token=abc");
        var provider = new ApiAuthenticationStateProvider(
            _httpClient, _localStorage, Substitute.For<ILogger<ApiAuthenticationStateProvider>>(), navigation);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
        _handler.SendCount.Should().Be(0, "the callback page handles its own token exchange");
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ShouldReturnStoredToken_WhenValid()
    {
        var token = TestJwt.Valid();
        await SeedTokenAsync(token);
        var provider = CreateProvider();

        var result = await provider.GetValidAccessTokenAsync();

        result.Token.Should().Be(token);
        result.Status.Should().Be(TokenRefreshStatus.Succeeded);
        _handler.SendCount.Should().Be(0);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ShouldRefresh_WhenForced_EvenIfStoredTokenStillValid()
    {
        await SeedTokenAsync(TestJwt.Valid());
        var newToken = TestJwt.Valid();
        _handler.EnqueueJson(HttpStatusCode.OK, TokenResponse(newToken));
        var provider = CreateProvider();

        var result = await provider.GetValidAccessTokenAsync(forceRefresh: true);

        result.Token.Should().Be(newToken);
        _handler.SendCount.Should().Be(1);
    }

    // The SemaphoreSlim + double-check must collapse a burst of callers hitting an
    // expired token into a single network refresh (no repeated token rotation).
    [Fact]
    public async Task GetValidAccessTokenAsync_ShouldCoalesceConcurrentRefreshes()
    {
        await SeedTokenAsync(TestJwt.Expired());
        var newToken = TestJwt.Valid();
        _handler.DelayMs = 50;
        _handler.EnqueueJson(HttpStatusCode.OK, TokenResponse(newToken));
        var provider = CreateProvider();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => provider.GetValidAccessTokenAsync()));

        results.Should().AllSatisfy(result => result.Token.Should().Be(newToken));
        _handler.SendCount.Should().Be(1, "concurrent callers should share a single refresh");
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ShouldReturnTransientFailure_WhenRefreshEndpointUnavailable()
    {
        await SeedTokenAsync(TestJwt.Expired());
        _handler.EnqueueStatus(HttpStatusCode.ServiceUnavailable);
        var provider = CreateProvider();

        var result = await provider.GetValidAccessTokenAsync();

        result.Token.Should().BeNull();
        result.Status.Should().Be(TokenRefreshStatus.TransientFailure);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ShouldReturnInvalidSession_WhenRefreshTokenRejected()
    {
        await SeedTokenAsync(TestJwt.Expired());
        _handler.EnqueueStatus(HttpStatusCode.BadRequest);
        var provider = CreateProvider();

        var result = await provider.GetValidAccessTokenAsync();

        result.Token.Should().BeNull();
        result.Status.Should().Be(TokenRefreshStatus.InvalidSession);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldKeepStoredToken_WhenRefreshFailsTransiently()
    {
        await SeedTokenAsync(TestJwt.Expired());
        _handler.EnqueueStatus(HttpStatusCode.ServiceUnavailable);
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
        (await StoredTokenAsync()).Should().NotBeNull("a transient failure must not wipe the session");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldWipeStoredToken_WhenSessionIsInvalid()
    {
        await SeedTokenAsync(TestJwt.Expired());
        _handler.EnqueueStatus(HttpStatusCode.BadRequest);
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
        (await StoredTokenAsync()).Should().BeNull("a rejected refresh token ends the session");
    }

    [Fact]
    public async Task MarkUserAsAuthenticatedAsync_ShouldStoreToken_AndRaiseStateChanged()
    {
        var provider = CreateProvider();
        var notified = false;
        provider.AuthenticationStateChanged += _ => notified = true;
        var token = TestJwt.Valid();

        await provider.MarkUserAsAuthenticatedAsync(token);

        (await StoredTokenAsync()).Should().Be(token);
        notified.Should().BeTrue();
    }

    [Fact]
    public async Task MarkUserAsLoggedOutAsync_ShouldRemoveToken_AndRaiseStateChanged()
    {
        _handler.FallbackStatus = HttpStatusCode.Unauthorized;
        await SeedTokenAsync(TestJwt.Valid());
        var provider = CreateProvider();
        var notified = false;
        provider.AuthenticationStateChanged += _ => notified = true;

        await provider.MarkUserAsLoggedOutAsync();

        (await StoredTokenAsync()).Should().BeNull();
        notified.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("header.payload")]
    [InlineData("...")]
    public async Task GetAuthenticationStateAsync_ShouldTreatAnUnreadableTokenAsInvalid(string storedToken)
    {
        // A corrupted or truncated value in local storage must not throw its way out of the
        // authentication state; it should read as "not signed in" and trigger a refresh attempt.
        _handler.FallbackStatus = HttpStatusCode.Unauthorized;
        await SeedTokenAsync(storedToken);
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().NotBe(true);
    }
}
