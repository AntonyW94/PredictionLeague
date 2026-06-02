using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace ThePredictions.Web.Client.Authentication;

/// <summary>
/// Attaches a fresh access token to every authenticated API call - refreshing it
/// first if it is missing or about to expire - so the session is extended on
/// activity rather than only on a hard reload. On a 401 it tries once to recover
/// the session with a forced refresh: if that succeeds it replays safe (bodyless)
/// requests and otherwise returns the 401 without disturbing the session; only if
/// the refresh itself fails does it record a logout message and throw
/// <see cref="SessionExpiredException"/> for the error boundary to handle.
/// </summary>
public class AuthorizationMessageHandler(
    IServiceProvider serviceProvider,
    SessionState sessionState,
    ILogger<AuthorizationMessageHandler> logger) : DelegatingHandler
{
    // Endpoints that authenticate via the refresh-token cookie (or are public).
    // They must not have a bearer token forced onto them and must not trigger the
    // refresh/redirect machinery, otherwise the refresh call would recurse.
    private static readonly string[] AnonymousPaths =
    [
        "api/authentication/login",
        "api/authentication/register",
        "api/authentication/refresh-token",
        "api/authentication/forgot-password",
        "api/authentication/reset-password"
    ];

    private const string LogoutMessage = "You've been logged out. Please log in again.";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsAnonymousPath(request))
            return await base.SendAsync(request, cancellationToken);

        // Resolved lazily (not via the constructor) to avoid a circular dependency:
        // the provider depends on the "API" HttpClient, whose handler chain includes
        // this handler. By the time a request flows the chain is already built.
        var provider = (ApiAuthenticationStateProvider)serviceProvider.GetRequiredService<AuthenticationStateProvider>();

        var (accessToken, _) = await provider.GetValidAccessTokenAsync();
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // The token was rejected (e.g. expired between sending and arriving, or
        // revoked). Try once to recover the session with a forced refresh.
        var (refreshedToken, status) = await provider.GetValidAccessTokenAsync(forceRefresh: true);

        if (!string.IsNullOrEmpty(refreshedToken))
        {
            // The session is still valid. A 401 means the server rejected the
            // request before running it, so a bodyless request is safe to replay
            // with the new token. A request with a body can't be safely re-sent,
            // so return the 401 and let the caller surface the failure - the
            // session itself is intact, so we must not log the user out.
            if (request.Content is null)
            {
                response.Dispose();
                using var retry = CloneRequest(request, refreshedToken);
                return await base.SendAsync(retry, cancellationToken);
            }

            return response;
        }

        // Couldn't refresh. Only end the session if the refresh token was
        // definitively rejected; a transient failure (API restarting during a
        // deploy, rate limit, network blip) must not log the user out - surface the
        // 401 for this one call and keep the session for the next attempt.
        if (status == TokenRefreshStatus.TransientFailure)
            return response;

        logger.LogInformation("Request to {Path} returned 401 and the session is no longer valid. Logging user out.", request.RequestUri);
        response.Dispose();

        sessionState.LogoutMessage = LogoutMessage;
        await provider.MarkUserAsLoggedOutAsync();
        throw new SessionExpiredException();
    }

    private static bool IsAnonymousPath(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        return AnonymousPaths.Any(anonymousPath => path.Contains(anonymousPath, StringComparison.OrdinalIgnoreCase));
    }

    // Builds a fresh copy of a bodyless request (the original cannot be re-sent
    // once it has been dispatched), preserving its headers and swapping in the
    // new bearer token. Credentials are re-applied downstream by CookieHandler.
    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, string accessToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        clone.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
        return clone;
    }
}
