using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace PredictionLeague.Web.Client.Authentication;

public class CookieHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"DEBUG: Sending request to: {request.RequestUri}");

        if (request.Headers.Any())
        {
            Console.WriteLine("DEBUG: Request Headers:");
            foreach (var header in request.Headers)
            {
                if (header.Key == "Authorization")
                    Console.WriteLine($"--> {header.Key}: {header.Value.FirstOrDefault()}");
            }
        }
        else
        {
            Console.WriteLine("DEBUG: No headers present on this request.");
        }
           
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return base.SendAsync(request, cancellationToken);
    }
}