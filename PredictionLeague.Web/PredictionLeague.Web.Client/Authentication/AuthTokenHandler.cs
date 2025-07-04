using Blazored.LocalStorage;
using System.Net.Http.Headers;

namespace PredictionLeague.Web.Client.Authentication;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;

    public AuthTokenHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Get the token from local storage
        var token = await _localStorage.GetItemAsync<string>("authToken", cancellationToken);

        // If a token exists, add it to the Authorization header for the outgoing request
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
        }

        // Continue with the request
        return await base.SendAsync(request, cancellationToken);
    }
}