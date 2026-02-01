# Domain Models

Domain entities follow specific construction and persistence patterns. These rules ensure consistency and maintain business invariants.

## Entity Construction

Domain entities have TWO construction paths:

### 1. Factory Method (`Create`) - For New Entities

Use when creating a NEW entity with business validation.

```csharp
public class League
{
    // Private parameterless constructor for factory method
    private League() { }

    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public int SeasonId { get; init; }
    public string AdministratorUserId { get; init; } = null!;
    public string EntryCode { get; init; } = null!;
    public DateTime CreatedAtUtc { get; init; }

    // Factory method - validates and creates
    public static League Create(int seasonId, string name, string administratorUserId)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(administratorUserId, nameof(administratorUserId));
        Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));

        return new League
        {
            SeasonId = seasonId,
            Name = name.Trim(),
            AdministratorUserId = administratorUserId,
            EntryCode = GenerateEntryCode(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static string GenerateEntryCode()
    {
        // 6-character alphanumeric
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
```

### 2. Public Constructor - For Database Hydration

Use when reconstructing an entity from the database. No validation - data is already validated.

```csharp
public class League
{
    // ... (private constructor and properties from above)

    // Public constructor for Dapper hydration
    public League(
        int id,
        string name,
        int seasonId,
        string administratorUserId,
        string entryCode,
        DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        SeasonId = seasonId;
        AdministratorUserId = administratorUserId;
        EntryCode = entryCode;
        CreatedAtUtc = createdAtUtc;
    }
}
```

### When to Use Which

| Scenario | Use |
|----------|-----|
| Creating a new entity in a command handler | `Entity.Create(...)` |
| Loading from database via repository | Public constructor (Dapper maps automatically) |
| Unit testing with known values | Public constructor |

## Repository Pattern

Repositories handle persistence and return domain entities.

### Create Operations - Return New Instance

**ALWAYS return a new instance after insert to preserve immutability.**

```csharp
public async Task<League> CreateAsync(League league, CancellationToken ct)
{
    const string sql = @"
        INSERT INTO [Leagues] ([Name], [SeasonId], [AdministratorUserId], [EntryCode], [CreatedAtUtc])
        OUTPUT INSERTED.[Id]
        VALUES (@Name, @SeasonId, @AdministratorUserId, @EntryCode, @CreatedAtUtc)";

    var newId = await _connection.ExecuteScalarAsync<int>(sql, new
    {
        league.Name,
        league.SeasonId,
        league.AdministratorUserId,
        league.EntryCode,
        league.CreatedAtUtc
    }, ct);

    // Return NEW instance with the generated ID
    return new League(
        id: newId,
        name: league.Name,
        seasonId: league.SeasonId,
        administratorUserId: league.AdministratorUserId,
        entryCode: league.EntryCode,
        createdAtUtc: league.CreatedAtUtc);
}
```

### Update Operations

For updates, you have two options depending on your needs:

**Option A: Return updated entity (when caller needs the result)**
```csharp
public async Task<League> UpdateAsync(League league, CancellationToken ct)
{
    const string sql = @"
        UPDATE [Leagues]
        SET [Name] = @Name, [UpdatedAtUtc] = @UpdatedAtUtc
        WHERE [Id] = @Id";

    await _connection.ExecuteAsync(sql, new
    {
        league.Id,
        league.Name,
        UpdatedAtUtc = DateTime.UtcNow
    }, ct);

    // Fetch and return the updated entity
    return await GetByIdAsync(league.Id, ct);
}
```

**Option B: Return void (when caller doesn't need the result)**
```csharp
public async Task UpdateNameAsync(int leagueId, string newName, CancellationToken ct)
{
    const string sql = @"
        UPDATE [Leagues]
        SET [Name] = @Name, [UpdatedAtUtc] = @UpdatedAtUtc
        WHERE [Id] = @Id";

    await _connection.ExecuteAsync(sql, new
    {
        Id = leagueId,
        Name = newName,
        UpdatedAtUtc = DateTime.UtcNow
    }, ct);
}
```

## Things to NEVER Do

### NEVER use reflection to set `init` properties

```csharp
// WRONG - Don't do this
var league = new League();
typeof(League).GetProperty("Id")!.SetValue(league, 123); // NEVER

// CORRECT - Use the constructor
var league = new League(id: 123, name: "My League", ...);
```

### NEVER bypass factory methods for new entities

```csharp
// WRONG - Bypasses validation
var league = new League(
    id: 0,  // ID should be assigned by database
    name: "",  // Empty name would be caught by Create()
    ...);

// CORRECT - Factory method validates
var league = League.Create(seasonId, name, userId);
```

### NEVER mutate entities after creation

Entities use `init` properties for immutability. If you need to change something, create a new instance or use specific update methods on the repository.

```csharp
// WRONG - Can't do this with init properties anyway
league.Name = "New Name";

// CORRECT - Use repository method
await _leagueRepository.UpdateNameAsync(league.Id, "New Name", ct);
```

## Rich Domain Models

Business logic belongs in domain entities, not services.

```csharp
public class Round
{
    public RoundStatus Status { get; init; }
    public DateTime DeadlineUtc { get; init; }

    // Business logic in the entity
    public bool CanAcceptPredictions()
    {
        return Status == RoundStatus.Published
            && DateTime.UtcNow < DeadlineUtc;
    }

    public bool IsCompleted()
    {
        return Status == RoundStatus.Completed;
    }
}

// Usage in handler
if (!round.CanAcceptPredictions())
{
    throw new InvalidOperationException("Round is not accepting predictions");
}
```
