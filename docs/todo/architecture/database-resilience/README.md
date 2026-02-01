# Database Resilience

## Status

**Not Started** | In Progress | Complete

## Summary

Improve database connection resilience with proper pooling configuration and retry policies for transient failures.

## Priority

**High** - Currently using defaults

## Requirements

### Connection Pooling Configuration
- [ ] Configure explicit pool size in connection string
- [ ] Set minimum pool size
- [ ] Set maximum pool size
- [ ] Configure connection lifetime

### Retry Policies
- [ ] Add retry policies for transient database failures
- [ ] Configure exponential backoff
- [ ] Handle specific SQL exceptions

## Connection String Configuration

```
Server=...;Database=...;Min Pool Size=5;Max Pool Size=100;Connection Lifetime=300;
```

## Retry Configuration

```csharp
// Using Polly
var retryPolicy = Policy
    .Handle<SqlException>(ex => IsTransient(ex))
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

## Related Items

- #54 Database Connection Pooling Configuration
- #56 Database Connection Resilience
