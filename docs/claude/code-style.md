# Code Style Guidelines

These rules apply to ALL code in the solution. Follow them without exception.

## UK English Spelling

**ALWAYS use UK English spelling throughout the codebase.**

| US English (WRONG) | UK English (CORRECT) |
|-------------------|---------------------|
| color | colour |
| center | centre |
| organize | organise |
| favorite | favourite |
| license (verb) | licence |
| analyze | analyse |
| canceled | cancelled |
| optimize | optimise |
| behavior | behaviour |

This applies to:
- File names (e.g., `colours.css` not `colors.css`)
- CSS class names
- Comments and documentation
- Variable names and string literals
- Error messages and UI text
- Log messages

```csharp
// CORRECT
var favouriteTeam = user.FavouriteTeam;
_logger.LogInformation("Colour scheme initialised");

// WRONG
var favoriteTeam = user.FavoriteTeam;
_logger.LogInformation("Color scheme initialized");
```

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `LeagueRepository` |
| Interfaces | IPascalCase | `ILeagueRepository` |
| Methods | PascalCase | `GetByIdAsync` |
| Public properties | PascalCase | `FirstName` |
| Private fields | _camelCase | `_connectionFactory` |
| Local variables | camelCase | `leagueId` |
| Constants | PascalCase | `MaxRetryCount` |
| Async methods | Async suffix | `CreateAsync` |
| Commands | Command suffix | `CreateLeagueCommand` |
| Queries | Query suffix | `GetMyLeaguesQuery` |
| Handlers | Handler suffix | `CreateLeagueCommandHandler` |
| DTOs | Dto suffix | `LeagueDto` |
| Validators | Validator suffix | `CreateLeagueCommandValidator` |

```csharp
// CORRECT
public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository _leagueRepository;

    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken ct)
    {
        var league = League.Create(request.Name);
        return await _leagueRepository.CreateAsync(league, ct);
    }
}

// WRONG - missing suffixes, wrong field naming
public class CreateLeague : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository leagueRepository; // Should be _leagueRepository

    public async Task<LeagueDto> Handle(...) // Method name is fine (inherited)
}
```

## File Organisation

**ALWAYS put one public class, record, or interface per file.**

```csharp
// CORRECT - Each type in its own file

// File: LeagueDto.cs
public record LeagueDto(int Id, string Name);

// File: LeagueMemberDto.cs
public record LeagueMemberDto(int Id, string UserId);

// WRONG - Multiple public types in one file
// File: LeagueDtos.cs
public record LeagueDto(int Id, string Name);
public record LeagueMemberDto(int Id, string UserId); // Should be in LeagueMemberDto.cs
```

**Exception:** Private nested classes within another class are allowed in the same file.

```csharp
// This is fine - private nested class
public class LeagueService
{
    private class LeagueCache  // Private nested class - OK in same file
    {
        // ...
    }
}
```

## Code Formatting

**ALWAYS put statements on a new line after `if`, `else`, `for`, `foreach`, `while`.**

```csharp
// CORRECT
if (!userExists)
    return;

if (condition)
    continue;

if (league == null)
{
    throw new EntityNotFoundException("League not found");
}

// WRONG - statement on same line
if (!userExists) return;
if (condition) continue;
if (league == null) throw new EntityNotFoundException("League not found");
```

## DateTime Handling

**ALWAYS use `DateTime.UtcNow`, NEVER use `DateTime.Now`.**

- All dates are stored in UTC in the database
- Property names use `Utc` suffix: `CreatedAtUtc`, `DeadlineUtc`, `UpdatedAtUtc`
- The `DapperUtcDateTimeHandler` ensures UTC kind on deserialization

```csharp
// CORRECT
var now = DateTime.UtcNow;
var deadline = new DateTime(2024, 3, 15, 14, 0, 0, DateTimeKind.Utc);
public DateTime CreatedAtUtc { get; init; }

// WRONG
var now = DateTime.Now;  // NEVER use this
var deadline = new DateTime(2024, 3, 15, 14, 0, 0);  // Missing DateTimeKind.Utc
public DateTime CreatedAt { get; init; }  // Missing Utc suffix
```

## Guard Clauses vs FluentValidation

Use the appropriate validation mechanism for each context:

| Context | Use | Example |
|---------|-----|---------|
| Domain entity factory methods | Ardalis.GuardClauses | `Guard.Against.NullOrWhiteSpace(name)` |
| Command/Query input validation | FluentValidation | `RuleFor(x => x.Name).NotEmpty()` |
| API controller parameters | FluentValidation (via pipeline) | Automatic via `ValidationBehaviour` |

```csharp
// Domain entity - use Guard clauses
public static League Create(string name, int seasonId)
{
    Guard.Against.NullOrWhiteSpace(name, nameof(name));
    Guard.Against.NegativeOrZero(seasonId, nameof(seasonId));

    return new League { Name = name, SeasonId = seasonId };
}

// Command validator - use FluentValidation
public class CreateLeagueCommandValidator : AbstractValidator<CreateLeagueCommand>
{
    public CreateLeagueCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SeasonId)
            .GreaterThan(0);
    }
}
```
