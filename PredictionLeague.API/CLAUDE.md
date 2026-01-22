# API Project - Project Guidelines

This file contains guidelines specific to the REST API project. For solution-wide patterns, see the root [CLAUDE.md](../CLAUDE.md).

## Controller Organisation

```
/api/auth           → Authentication (login, register, refresh)
/api/account        → User profile
/api/dashboard      → Dashboard data
/api/leagues        → League CRUD and membership
/api/predictions    → Prediction submission
/api/rounds         → Round queries
/api/admin/rounds   → Admin round management
/api/admin/seasons  → Admin season management
/api/tasks          → Background job triggers (API key protected)
```

## Authentication

- JWT Bearer tokens with 60-minute expiry
- Refresh tokens stored in HTTP-only cookies (7-day expiry)
- Google OAuth for social login
- API key authentication for scheduled tasks (`X-Api-Key` header)

## Error Handling

### ErrorHandlingMiddleware

Maps exceptions to HTTP status codes:

| Exception Type | Status Code |
|---------------|-------------|
| `KeyNotFoundException`, `EntityNotFoundException` | 404 |
| `ArgumentException`, `InvalidOperationException` | 400 |
| `ValidationException` (FluentValidation) | 400 |
| `UnauthorizedAccessException` | 401 |
| Other exceptions | 500 |

## Scheduled Task Endpoints

All `/api/tasks/*` endpoints are protected by API key (`X-Api-Key` header).

| Endpoint | Purpose |
|----------|---------|
| `/api/tasks/publish-upcoming-rounds` | Publish rounds that are ready |
| `/api/tasks/send-reminders` | Send prediction reminder emails |
| `/api/tasks/sync-season` | Sync season data from external API |
| `/api/tasks/update-live-scores` | Update match scores during games |
