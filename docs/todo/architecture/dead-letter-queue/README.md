# Dead Letter Queue

## Status

**Not Started** | In Progress | Complete

## Summary

Persist failed jobs for retry and investigation.

## Priority

**Low** - Post-launch improvement

## Requirements

- [ ] Create dead letter queue storage
- [ ] Capture failed email sends
- [ ] Capture failed API sync operations
- [ ] Provide admin interface to view/retry failed jobs
- [ ] Configure retention period

## Technical Notes

Options:
- Database table for simplicity
- Azure Service Bus (if scaling needed)
- Hangfire for job scheduling with retry

## Schema

```sql
CREATE TABLE [DeadLetterQueue] (
    [Id] INT IDENTITY PRIMARY KEY,
    [JobType] NVARCHAR(100) NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [ErrorMessage] NVARCHAR(MAX) NOT NULL,
    [FailedAtUtc] DATETIME2 NOT NULL,
    [RetryCount] INT NOT NULL DEFAULT 0,
    [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending'
);
```
