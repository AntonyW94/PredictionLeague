using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Web.Client.Authentication;
using ThePredictions.Web.Client.Tests.Unit.TestDoubles;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Authentication;

public class AuthorizationMessageHandlerTests
{
    private const string AccessTokenKey = "accessToken";

    private readonly InMemoryLocalStorage _localStorage = new();
    private readonly StubHttpMessageHandler _refreshHandler = new();   // serves the provider's refresh calls
    private readonly StubHttpMessageHandler _apiHandler = new();       // serves the actual API call + replay
    private readonly SessionState _sessionState = new();
    private readonly HttpMessageInvoker _invoker;

    public AuthorizationMessageHandlerTests()
    {
        var refreshClient = new HttpClient(_refreshHandler) { BaseAddress = new Uri("https://localhost/") };
        var provider = new ApiAuthenticationStateProvider(
            refreshClient, _localStorage, Substitute.For<ILogger<ApiAuthenticationStateProvider>>(), new TestNavigationManager());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(AuthenticationStateProvider)).Returns(provider);

        var handler = new AuthorizationMessageHandler(
            serviceProvider, _sessionState, Substitute.For<ILogger<AuthorizationMessageHandler>>())
        {
            InnerHandler = _apiHandler
        };
        _invoker = new HttpMessageInvoker(handler);
    }

    private static SuccessfulAuthenticationResponse TokenResponse(string accessToken) =>
        new(accessToken, DateTime.UtcNow.AddMinutes(15), "refresh-token");

    private Task SeedTokenAsync(string token) =>
        _localStorage.SetItemAsync(AccessTokenKey, token, CancellationToken.None).AsTask();

    private Task<string?> StoredTokenAsync() =>
        _localStorage.GetItemAsync<string>(AccessTokenKey, CancellationToken.None).AsTask();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        return _invoker.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task SendAsync_ShouldAttachBearerToken_ForAuthenticatedRequest()
    {
        var token = TestJwt.Valid();
        await SeedTokenAsync(token);
        _apiHandler.EnqueueStatus(HttpStatusCode.OK);

        var response = await SendAsync(HttpMethod.Get, "https://localhost/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _apiHandler.Requests.Should().ContainSingle();
        _apiHandler.Requests[0].BearerToken.Should().Be(token);
    }

    [Fact]
    public async Task SendAsync_ShouldNotAttachToken_ForAnonymousAuthEndpoint()
    {
        await SeedTokenAsync(TestJwt.Valid());
        _apiHandler.EnqueueStatus(HttpStatusCode.OK);

        await SendAsync(HttpMethod.Post, "https://localhost/api/authentication/login");

        _apiHandler.Requests[0].BearerToken.Should().BeNull("auth endpoints authenticate via the cookie, not a bearer token");
        _refreshHandler.SendCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_ShouldRefreshAndReplay_When401OnBodylessRequest_AndRefreshSucceeds()
    {
        await SeedTokenAsync(TestJwt.Valid());
        var newToken = TestJwt.Valid();
        _apiHandler.EnqueueStatus(HttpStatusCode.Unauthorized).EnqueueStatus(HttpStatusCode.OK);
        _refreshHandler.EnqueueJson(HttpStatusCode.OK, TokenResponse(newToken));

        var response = await SendAsync(HttpMethod.Get, "https://localhost/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _apiHandler.SendCount.Should().Be(2);
        _apiHandler.Requests[1].BearerToken.Should().Be(newToken, "the replay must use the freshly refreshed token");
        _refreshHandler.SendCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_ShouldReturn401WithoutLoggingOut_When401OnRequestWithBody_AndRefreshSucceeds()
    {
        await SeedTokenAsync(TestJwt.Valid());
        _apiHandler.EnqueueStatus(HttpStatusCode.Unauthorized);
        _refreshHandler.EnqueueJson(HttpStatusCode.OK, TokenResponse(TestJwt.Valid()));

        var response = await SendAsync(HttpMethod.Post, "https://localhost/api/leagues", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _apiHandler.SendCount.Should().Be(1, "a request with a body must not be replayed");
        _sessionState.LogoutMessage.Should().BeNull("the session is still valid, so the user must not be logged out");
    }

    [Fact]
    public async Task SendAsync_ShouldThrowSessionExpiredAndLogOut_When401_AndRefreshTokenRejected()
    {
        await SeedTokenAsync(TestJwt.Valid());
        _apiHandler.EnqueueStatus(HttpStatusCode.Unauthorized);
        _refreshHandler.EnqueueStatus(HttpStatusCode.BadRequest);   // refresh token rejected => session over

        var act = async () => await SendAsync(HttpMethod.Get, "https://localhost/api/leagues");

        await act.Should().ThrowAsync<SessionExpiredException>();
        _sessionState.LogoutMessage.Should().NotBeNullOrEmpty();
        (await StoredTokenAsync()).Should().BeNull("the stale token should be cleared on logout");
    }

    [Fact]
    public async Task SendAsync_ShouldReturn401WithoutLoggingOut_When401_AndRefreshFailsTransiently()
    {
        await SeedTokenAsync(TestJwt.Valid());
        _apiHandler.EnqueueStatus(HttpStatusCode.Unauthorized);
        _refreshHandler.EnqueueStatus(HttpStatusCode.ServiceUnavailable);   // API restarting / unreachable

        var response = await SendAsync(HttpMethod.Get, "https://localhost/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _sessionState.LogoutMessage.Should().BeNull("a transient refresh failure must not log the user out");
        (await StoredTokenAsync()).Should().NotBeNull("the session must be preserved across a transient failure");
    }
}
