# Request Timeout Configuration

## Status

**Not Started** | In Progress | Complete

## Summary

Configure explicit timeouts on HttpClient and database queries to prevent hanging requests.

## Priority

**High** - No explicit timeouts currently configured

## Requirements

### HttpClient Timeouts
- [ ] Configure timeout on Football API HttpClient
- [ ] Configure timeout on email service HttpClient
- [ ] Set appropriate values based on expected response times

### Database Query Timeouts
- [ ] Configure command timeout in Dapper queries
- [ ] Set appropriate values for long-running queries

## Recommended Timeouts

| Operation | Timeout |
|-----------|---------|
| Football API calls | 30 seconds |
| Email service calls | 15 seconds |
| Standard database queries | 30 seconds |
| Long-running reports | 120 seconds |

## Technical Notes

```csharp
// HttpClient
services.AddHttpClient("FootballApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Dapper
await connection.QueryAsync<T>(sql, parameters, commandTimeout: 30);
```
