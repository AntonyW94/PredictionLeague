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

    /// <summary>Simulates the API being unreachable, e.g. mid-deploy.</summary>
    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private ApiAuthenticationStateProvider CreateUnreachableProvider()
    {
        var client = new HttpClient(new UnreachableHandler()) { BaseAddress = new Uri("https://localhost/") };
        return new ApiAuthenticationStateProvider(client, _localStorage, Substitute.For<ILogger<ApiAuthenticationStateProvider>>(), _navigation);
    }

    // ---------- refresh failure modes ----------

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldKeepTheStoredToken_WhenTheApiIsUnreachable()
    {
        // A refresh that cannot reach the API is transient: the refresh token may still be good,
        // so the session must survive a deploy rather than silently logging everyone out.
        await SeedTokenAsync(TestJwt.Expired());
        var provider = CreateUnreachableProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().NotBe(true);
        (await StoredTokenAsync()).Should().NotBeNull("a transient failure must not discard the session");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldStayAnonymous_WhenTheRefreshReturnsNoToken()
    {
        await SeedTokenAsync(TestJwt.Expired());
        _handler.EnqueueJson(HttpStatusCode.OK, new { AccessToken = (string?)null, ExpiresAtUtc = DateTime.UtcNow, RefreshTokenForCookie = "x" });
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().NotBe(true);
    }

    // ---------- logging in from the URL token ----------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task LoginWithRefreshToken_ShouldRefuse_WhenTheTokenIsMissing(string? token)
    {
        var provider = CreateProvider();

        (await provider.LoginWithRefreshToken(token!)).Should().BeFalse();
        _handler.SendCount.Should().Be(0);
    }

    [Fact]
    public async Task LoginWithRefreshToken_ShouldRefuse_WhenTheApiRejectsTheToken()
    {
        _handler.EnqueueStatus(HttpStatusCode.Unauthorized);
        var provider = CreateProvider();

        (await provider.LoginWithRefreshToken("some-token")).Should().BeFalse();
        (await StoredTokenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task LoginWithRefreshToken_ShouldRefuse_WhenTheResponseCannotBeRead()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, null!);
        var provider = CreateProvider();

        (await provider.LoginWithRefreshToken("some-token")).Should().BeFalse();
    }

    [Fact]
    public async Task LoginWithRefreshToken_ShouldStoreTheTokenAndSignTheUserIn()
    {
        var accessToken = TestJwt.Valid(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        _handler.EnqueueJson(HttpStatusCode.OK, TokenResponse(accessToken));
        var provider = CreateProvider();
        var notified = false;
        provider.AuthenticationStateChanged += _ => notified = true;

        var result = await provider.LoginWithRefreshToken("some-token");

        result.Should().BeTrue();
        (await StoredTokenAsync()).Should().Be(accessToken);
        notified.Should().BeTrue();
    }

    [Fact]
    public async Task LoginWithRefreshToken_ShouldRestoreSpacesToPluses()
    {
        // A base64 refresh token arriving in a URL has its '+' characters turned into spaces.
        _handler.EnqueueJson(HttpStatusCode.OK, TokenResponse(TestJwt.Valid()));
        var provider = CreateProvider();

        await provider.LoginWithRefreshToken("a b c");

        _handler.SendCount.Should().Be(1);
    }

    /// <summary>Local storage that fails, as a browser with storage disabled or quota-full does.</summary>
    private sealed class FailingLocalStorage : Blazored.LocalStorage.ILocalStorageService
    {
        public ValueTask<T?> GetItemAsync<T>(string key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("local storage unavailable");

        public ValueTask SetItemAsync<T>(string key, T data, CancellationToken cancellationToken = default) => default;
        public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default) => default;
        public ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) => default;
        public ValueTask ClearAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask<int> LengthAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask<string?> KeyAsync(int index, CancellationToken cancellationToken = default) => default;
        public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask<bool> ContainKeyAsync(string key, CancellationToken cancellationToken = default) => default;
        public ValueTask<string?> GetItemAsStringAsync(string key, CancellationToken cancellationToken = default) => default;
        public ValueTask SetItemAsStringAsync(string key, string data, CancellationToken cancellationToken = default) => default;

        public event EventHandler<Blazored.LocalStorage.ChangingEventArgs>? Changing { add { } remove { } }
        public event EventHandler<Blazored.LocalStorage.ChangedEventArgs>? Changed { add { } remove { } }
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymous_WhenReadingTheTokenThrows()
    {
        // A browser with storage blocked must land on the sign-in page, not crash the app shell.
        var provider = new ApiAuthenticationStateProvider(
            _httpClient, new FailingLocalStorage(), Substitute.For<ILogger<ApiAuthenticationStateProvider>>(), _navigation);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().NotBe(true);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldStayAnonymous_WhenTheRefreshBodyIsNull()
    {
        await SeedTokenAsync(TestJwt.Expired());
        _handler.EnqueueJson(HttpStatusCode.OK, null!);
        var provider = CreateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().NotBe(true);
    }
}
