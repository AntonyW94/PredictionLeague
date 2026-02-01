# Distributed Tracing / Correlation IDs

## Status

**Not Started** | In Progress | Complete

## Summary

Implement correlation IDs for request tracking across the application stack.

## Priority

**High** - Aids debugging and incident response

## Requirements

- [ ] Generate correlation ID for each request
- [ ] Pass correlation ID through all log statements
- [ ] Include correlation ID in API responses (header)
- [ ] Include correlation ID in error responses
- [ ] Store correlation ID in Datadog traces

## Technical Notes

Options:
- Use `Activity.Current?.Id` from System.Diagnostics
- Custom middleware to generate/propagate correlation ID
- Serilog enrichment for automatic inclusion

## Implementation

```csharp
// Middleware to ensure correlation ID
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-ID", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```
