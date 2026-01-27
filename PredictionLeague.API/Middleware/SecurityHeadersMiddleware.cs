namespace PredictionLeague.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Prevent clickjacking
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // Legacy XSS protection (for older browsers)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Control referrer information
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // HSTS - Force HTTPS for 1 year, including subdomains
        // Only add for HTTPS requests to avoid issues during development
        if (context.Request.IsHttps)
        {
            context.Response.Headers.Append(
                "Strict-Transport-Security",
                "max-age=31536000; includeSubDomains");
        }

        // Restrict browser features the application doesn't need
        context.Response.Headers.Append("Permissions-Policy",
            "accelerometer=(), " +
            "camera=(), " +
            "geolocation=(), " +
            "gyroscope=(), " +
            "magnetometer=(), " +
            "microphone=(), " +
            "payment=(), " +
            "usb=()");

        // Content Security Policy
        // Configured for Blazor WASM compatibility:
        // - 'wasm-unsafe-eval' required for WebAssembly execution
        // - 'unsafe-inline' for styles as Blazor may inject inline styles
        // - data: for fonts/images as Blazor may use data URIs
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'wasm-unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' https://accounts.google.com; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "base-uri 'self'; " +
            "upgrade-insecure-requests;");

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
