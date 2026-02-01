# Health Check Endpoints

## Status

**Not Started** | In Progress | Complete

## Summary

Implement ASP.NET Core health checks for monitoring application health, including database connectivity and external API availability.

## Priority

**Critical** - Required for production monitoring

## Requirements

- [ ] Add `/health` endpoint for basic health status
- [ ] Add `/healthz` endpoint for Kubernetes-style probes (if needed)
- [ ] Check database connectivity
- [ ] Check Football API availability
- [ ] Return appropriate HTTP status codes (200 OK, 503 Service Unavailable)

## Technical Notes

Use ASP.NET Core Health Checks:
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `AspNetCore.HealthChecks.SqlServer` for database checks

## References

- [ASP.NET Core Health Checks Documentation](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
