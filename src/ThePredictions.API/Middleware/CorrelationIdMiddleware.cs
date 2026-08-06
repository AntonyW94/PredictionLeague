using System.Diagnostics.CodeAnalysis;
using Serilog.Context;

namespace ThePredictions.API.Middleware;

[ExcludeFromCodeCoverage(Justification = "Middleware wiring: registration and header plumbing, exercised end to end.")]
public class CorrelationIdMiddleware(RequestDelegate next)
{
    internal const string HeaderName = "X-Correlation-Id";
    internal const string LogPropertyName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items[LogPropertyName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await next(context);
        }
    }
}
