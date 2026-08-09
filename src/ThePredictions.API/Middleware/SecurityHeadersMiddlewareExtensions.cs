using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.API.Middleware;

[ExcludeFromCodeCoverage(Justification = "Middleware registration: one UseMiddleware call, exercised end to end.")]
public static class SecurityHeadersMiddlewareExtensions
{
    public static void UseSecurityHeaders(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
