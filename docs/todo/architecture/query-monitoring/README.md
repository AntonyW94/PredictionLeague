# Query Performance Monitoring

## Status

**Not Started** | In Progress | Complete

## Summary

Track slow queries and identify missing indexes for database performance optimisation.

## Priority

**Medium** - No query performance tracking currently

## Requirements

- [ ] Log slow queries (> 1 second)
- [ ] Track query execution times
- [ ] Identify missing indexes
- [ ] Set up alerts for slow queries
- [ ] Create performance dashboard

## Technical Notes

Options:
- Serilog query timing middleware
- Datadog APM (if upgraded)
- SQL Server Query Store (if available on Fasthosts)

## Implementation

```csharp
// Simple query timing with Serilog
var sw = Stopwatch.StartNew();
var result = await connection.QueryAsync<T>(sql, parameters);
sw.Stop();

if (sw.ElapsedMilliseconds > 1000)
{
    _logger.LogWarning("Slow query ({ElapsedMs}ms): {Sql}", sw.ElapsedMilliseconds, sql);
}
```
