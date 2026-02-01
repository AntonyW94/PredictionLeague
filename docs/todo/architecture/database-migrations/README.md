# Database Migrations System

## Status

**Not Started** | In Progress | Complete

## Summary

Implement version-controlled database schema changes for safer deployments.

## Priority

**Medium** - Currently no version-controlled schema changes

## Requirements

- [ ] Choose migration tool (EF Core Migrations, DbUp, FluentMigrator)
- [ ] Set up migration project structure
- [ ] Create baseline migration from current schema
- [ ] Integrate migrations into deployment process
- [ ] Document rollback procedures

## Options to Consider

| Tool | Pros | Cons |
|------|------|------|
| DbUp | Simple, SQL-based | Manual SQL writing |
| FluentMigrator | Fluent API, rollback support | Learning curve |
| EF Core Migrations | Integrated with EF | We use Dapper |

## Recommendation

DbUp is likely the best fit since we use Dapper and raw SQL. It's simple and allows us to version control our SQL scripts.

## Technical Notes

```csharp
// DbUp example
var upgrader = DeployChanges.To
    .SqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
```
