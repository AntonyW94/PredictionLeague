using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace ThePredictions.Web.Client.Authentication;

/// <summary>
/// Attaches a fresh access token to every authenticated API call - refreshing it
/// first if it is missing or about to expire - so the session is extended on
/// activity rather than only on a hard reload. On a 401 it makes a single refresh
/// + retry attempt for safe (bodyless) requests; if the user genuinely can no
/// longer be authenticated it records a logout message and throws
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

        var accessToken = await provider.GetValidAccessTokenAsync();
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // The token was rejected (e.g. revoked by a concurrent refresh in another
        // tab). For a request with no body we can safely re-issue it once with a
        // freshly forced token.
        if (request.Content is null)
        {
            var refreshedToken = await provider.GetValidAccessTokenAsync(forceRefresh: true);
            if (!string.IsNullOrEmpty(refreshedToken))
            {
                response.Dispose();

                using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
                retry.Version = request.Version;
                retry.Headers.Authorization = new AuthenticationHeaderValue("bearer", refreshedToken);

                response = await base.SendAsync(retry, cancellationToken);
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                    return response;
            }
        }

        logger.LogInformation("Request to {Path} returned 401 and could not be re-authenticated. Logging user out.", request.RequestUri);
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
}
