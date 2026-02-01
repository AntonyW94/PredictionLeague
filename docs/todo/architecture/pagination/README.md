# Pagination on List Endpoints

## Status

**Not Started** | In Progress | Complete

## Summary

Add pagination to list endpoints that currently return full datasets.

## Priority

**High** - All queries return full datasets currently

## Affected Endpoints

- [ ] `GetMyLeagues` - Returns all user's leagues
- [ ] `FetchAllTeams` - Returns all teams
- [ ] Leaderboard endpoints - Return all members
- [ ] Admin user list - Returns all users

## Requirements

- [ ] Define standard pagination parameters (`page`, `pageSize`)
- [ ] Define standard pagination response format
- [ ] Update affected query handlers
- [ ] Update API endpoints
- [ ] Update Blazor components to handle pagination

## Pagination Response Format

```csharp
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

## Technical Notes

- Default page size: 20
- Maximum page size: 100
- Use `OFFSET`/`FETCH` in SQL queries
