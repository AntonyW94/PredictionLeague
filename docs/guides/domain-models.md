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
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    // Pure static method - generates a random code (uniqueness checked by command handler)
    public static string GenerateRandomEntryCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public void SetEntryCode(string entryCode)
    {
        Guard.Against.NullOrWhiteSpace(entryCode);
        EntryCode = entryCode;
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

## Validation: Guard Clauses vs FluentValidation

This project uses TWO validation mechanisms with distinct purposes:

### FluentValidation - Application Layer (User Input)

Use for validating **user input** at the application boundary. Collects all errors and returns user-friendly messages.

```csharp
// Location: PredictionLeague.Validators/CreateLeagueCommandValidator.cs
public class CreateLeagueCommandValidator : AbstractValidator<CreateLeagueCommand>
{
    public CreateLeagueCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("League name is required")
            .MaximumLength(100).WithMessage("League name cannot exceed 100 characters");

        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("Please select a valid season");
    }
}
```

**Characteristics:**
- Validates Commands and Queries (DTOs)
- Collects ALL errors before returning
- User-friendly, localisable messages
- Runs via MediatR pipeline behaviour

### Guard Clauses - Domain Layer (Invariant Protection)

Use for protecting **domain invariants** in entity construction. Throws immediately on first violation.

```csharp
// Location: PredictionLeague.Domain/Entities/League.cs
public static League Create(int seasonId, string name, string administratorUserId)
{
    // Guard clauses protect domain invariants
    Guard.Against.NullOrWhiteSpace(name, nameof(name));
    Guard.Against.NullOrWhiteSpace(administratorUserId, nameof(administratorUserId));
    Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));

    return new League { ... };
}
```

**Characteristics:**
- Protects domain entity invariants
- Throws on FIRST violation (fail fast)
- Developer-facing messages (programming errors)
- Should NEVER fail if FluentValidation passed

### When to Use Which

| Layer | Mechanism | Purpose | Failure Means |
|-------|-----------|---------|---------------|
| Application (Commands/Queries) | FluentValidation | Validate user input | User provided bad data |
| Domain (Entities/Value Objects) | Guard Clauses | Protect invariants | Bug in calling code |

### The Validation Flow

```
API Request
    ↓
FluentValidation (MediatR pipeline)
    → Returns 400 Bad Request with all errors if invalid
    ↓
Command Handler
    ↓
Domain Entity Factory (e.g., League.Create())
    → Guard clauses protect invariants
    → Should NEVER fail (data already validated)
    → If fails = bug in code, not user error
```

### Overlap is Intentional

Guard clauses often check the same things as FluentValidation. This is **intentional defence in depth**:

```csharp
// FluentValidation checks this at the boundary
RuleFor(x => x.Name).NotEmpty();

// Guard clauses check again in the domain (safety net)
Guard.Against.NullOrWhiteSpace(name, nameof(name));
```

Why both?
- FluentValidation might be bypassed (internal calls, tests)
- Domain must protect its own invariants
- Different error handling (user feedback vs fail fast)

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

### Update Operations - Prefer Specific Methods

**PREFER specific, intention-revealing update methods over generic `UpdateAsync(entity)`.**

```csharp
// PREFERRED - Clear intent, explicit about what changes
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

public async Task UpdateStatusAsync(int roundId, RoundStatus status, CancellationToken ct)
{
    const string sql = @"
        UPDATE [Rounds]
        SET [Status] = @Status, [UpdatedAtUtc] = @UpdatedAtUtc
        WHERE [Id] = @Id";

    await _connection.ExecuteAsync(sql, new
    {
        Id = roundId,
        Status = status,
        UpdatedAtUtc = DateTime.UtcNow
    }, ct);
}
```

**Why specific methods?**
- Clear intent - obvious what the operation changes
- Prevents accidental overwrites of unrelated fields
- Better for audit logging and debugging
- Follows CQRS principle of explicit commands
- Easier to optimise (only updates needed columns)

**When to return the updated entity:**

Only return the entity if the caller genuinely needs it (e.g., to return database-generated values):

```csharp
// Return entity when caller needs database-generated values
public async Task<Round> UpdateStatusAsync(int roundId, RoundStatus status, CancellationToken ct)
{
    const string sql = @"
        UPDATE [Rounds]
        SET [Status] = @Status, [UpdatedAtUtc] = @UpdatedAtUtc
        WHERE [Id] = @Id";

    await _connection.ExecuteAsync(sql, new
    {
        Id = roundId,
        Status = status,
        UpdatedAtUtc = DateTime.UtcNow
    }, ct);

    return await GetByIdAsync(roundId, ct);
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

Entities use `init` properties for immutability. If you need to change something, use specific repository update methods.

```csharp
// WRONG - Can't do this with init properties anyway
league.Name = "New Name";

// CORRECT - Use repository method
await _leagueRepository.UpdateNameAsync(league.Id, "New Name", ct);
```

### NEVER use generic UpdateAsync with full entity

```csharp
// AVOID - Unclear what changed, risk of accidental overwrites
public async Task UpdateAsync(League league, CancellationToken ct)
{
    // Updates ALL fields - dangerous
}

// PREFER - Explicit about what changes
public async Task UpdateNameAsync(int leagueId, string name, CancellationToken ct)
{
    // Only updates name - safe and clear
}
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
