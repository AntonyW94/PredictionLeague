# Task: Introduce IDateTimeProvider

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Replace all direct `DateTime.UtcNow` calls in the domain layer with an injectable `IDateTimeProvider`. This enables unit tests to control time for predictable, deterministic outcomes — removing flaky assertions like "roughly now" and allowing exact timestamp verification, deadline boundary testing, and token expiry logic.

## Files to Create

| File | Purpose |
|------|---------|
| `src/PredictionLeague.Domain/Common/IDateTimeProvider.cs` | Interface definition |
| `src/PredictionLeague.Infrastructure/DateTimeProvider.cs` | Production implementation returning `DateTime.UtcNow` |

## Files to Modify

| File | Change Summary |
|------|---------------|
| `src/PredictionLeague.Domain/Models/League.cs` | Add `IDateTimeProvider` param to `Create`, `CreateOfficialPublicLeague`, `AddMember`; pass through to `Validate` |
| `src/PredictionLeague.Domain/Models/UserPrediction.cs` | Add `IDateTimeProvider` param to `Create` and `SetOutcome` |
| `src/PredictionLeague.Domain/Models/LeagueMember.cs` | Add `IDateTimeProvider` param to `Create` and `Approve` |
| `src/PredictionLeague.Domain/Models/Round.cs` | Add `IDateTimeProvider` param to `UpdateStatus` and `UpdateLastReminderSent` |
| `src/PredictionLeague.Domain/Models/Winning.cs` | Add `IDateTimeProvider` param to `Create` |
| `src/PredictionLeague.Domain/Models/PasswordResetToken.cs` | Add `IDateTimeProvider` param to `Create` (token string is already a parameter); convert `IsExpired` property to method |
| `src/PredictionLeague.Domain/Models/RefreshToken.cs` | Convert `IsExpired` and `IsActive` properties to methods; add `IDateTimeProvider` param to `Revoke` |
| `src/PredictionLeague.Domain/Services/PredictionDomainService.cs` | Inject `IDateTimeProvider` via constructor |
| `src/PredictionLeague.Infrastructure/DependencyInjection.cs` | Register `IDateTimeProvider` as singleton |
| Command handlers that call affected methods | Pass `IDateTimeProvider` through (cascading changes) |

## Implementation Steps

### Step 1: Create the IDateTimeProvider interface

Create `src/PredictionLeague.Domain/Common/IDateTimeProvider.cs`:

```csharp
namespace PredictionLeague.Domain.Common;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
```

### Step 2: Create the production implementation

Create `src/PredictionLeague.Infrastructure/DateTimeProvider.cs`:

```csharp
using PredictionLeague.Domain.Common;

namespace PredictionLeague.Infrastructure;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

### Step 3: Register in DI

In `src/PredictionLeague.Infrastructure/DependencyInjection.cs`, add:

```csharp
services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
```

### Step 4: Update entity factory methods

Add `IDateTimeProvider dateTimeProvider` as the **last parameter** to each factory method. Replace `DateTime.UtcNow` with `dateTimeProvider.UtcNow`.

#### League.cs

```csharp
// Create — add parameter, use in CreatedAtUtc assignment and Validate call
public static League Create(
    int seasonId,
    string name,
    decimal price,
    DateTime entryDeadlineUtc,
    string administratorUserId,
    IDateTimeProvider dateTimeProvider)  // NEW
{
    // ...
    CreatedAtUtc = dateTimeProvider.UtcNow,  // was DateTime.UtcNow
    // ...
    Validate(entryDeadlineUtc, dateTimeProvider);  // pass through
}

// CreateOfficialPublicLeague — same pattern, add parameter and pass to Validate
public static League CreateOfficialPublicLeague(..., IDateTimeProvider dateTimeProvider)

// Validate (private) — add parameter
private static void Validate(DateTime entryDeadlineUtc, IDateTimeProvider dateTimeProvider)
{
    Guard.Against.Expression(d => d <= dateTimeProvider.UtcNow, entryDeadlineUtc, ...);
}

// AddMember — add parameter for deadline check
public LeagueMember AddMember(string userId, IDateTimeProvider dateTimeProvider)
{
    if (EntryDeadlineUtc < dateTimeProvider.UtcNow)  // was DateTime.UtcNow
        throw new InvalidOperationException(...);
    // ...
}
```

#### UserPrediction.cs

```csharp
// Create — add parameter
public static UserPrediction Create(..., IDateTimeProvider dateTimeProvider)
{
    var nowUtc = dateTimeProvider.UtcNow;  // was DateTime.UtcNow
    // ...
}

// SetOutcome — add parameter (UpdatedAtUtc set in both branches)
public void SetOutcome(..., IDateTimeProvider dateTimeProvider)
{
    // ...
    UpdatedAtUtc = dateTimeProvider.UtcNow;  // was DateTime.UtcNow (both branches)
}
```

#### LeagueMember.cs

```csharp
// Create — add parameter
public static LeagueMember Create(int leagueId, string userId, IDateTimeProvider dateTimeProvider)
{
    // ...
    JoinedAtUtc = dateTimeProvider.UtcNow,  // was DateTime.UtcNow
    // ...
}

// Approve — add parameter
public void Approve(IDateTimeProvider dateTimeProvider)
{
    // ...
    ApprovedAtUtc = dateTimeProvider.UtcNow;  // was DateTime.UtcNow
}
```

#### Round.cs

```csharp
// UpdateStatus — add parameter
public void UpdateStatus(RoundStatus status, IDateTimeProvider dateTimeProvider)
{
    // ...
    CompletedDateUtc = dateTimeProvider.UtcNow;  // was DateTime.UtcNow
    // ...
}

// UpdateLastReminderSent — add parameter
public void UpdateLastReminderSent(IDateTimeProvider dateTimeProvider)
{
    LastReminderSentUtc = dateTimeProvider.UtcNow;  // was DateTime.UtcNow
}
```

#### Winning.cs

```csharp
// Create — add parameter
public static Winning Create(..., IDateTimeProvider dateTimeProvider)
{
    // ...
    AwardedDateUtc = dateTimeProvider.UtcNow,  // was DateTime.UtcNow
    // ...
}
```

### Step 5: Update token models

These require converting properties to methods since `IsExpired` cannot accept parameters.

#### PasswordResetToken.cs

```csharp
// Create — add IDateTimeProvider parameter (token string is already accepted as a parameter)
public static PasswordResetToken Create(string token, string userId, IDateTimeProvider dateTimeProvider, int expiryHours = 1)
{
    var now = dateTimeProvider.UtcNow;  // was DateTime.UtcNow
    // ...
}

// IsExpired — convert property to method
// BEFORE: public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc;
// AFTER:
public bool IsExpired(IDateTimeProvider dateTimeProvider) => dateTimeProvider.UtcNow > ExpiresAtUtc;
```

#### RefreshToken.cs

```csharp
// IsExpired — convert property to method
// BEFORE: public bool IsExpired => DateTime.UtcNow >= Expires;
// AFTER:
public bool IsExpired(IDateTimeProvider dateTimeProvider) => dateTimeProvider.UtcNow >= Expires;

// IsActive — convert property to method (depends on IsExpired)
// BEFORE: public bool IsActive => Revoked == null && !IsExpired;
// AFTER:
public bool IsActive(IDateTimeProvider dateTimeProvider) => Revoked == null && !IsExpired(dateTimeProvider);

// Revoke — add parameter
public void Revoke(IDateTimeProvider dateTimeProvider)
{
    Revoked = dateTimeProvider.UtcNow;  // was DateTime.UtcNow
}
```

### Step 6: Update PredictionDomainService

Inject `IDateTimeProvider` via constructor:

```csharp
public class PredictionDomainService
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public PredictionDomainService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public IEnumerable<UserPrediction> SubmitPredictions(
        Round round,
        string userId,
        IEnumerable<(int MatchId, int HomeScore, int AwayScore)> predictedScores)
    {
        Guard.Against.Null(round);

        if (round.DeadlineUtc < _dateTimeProvider.UtcNow)  // was DateTime.UtcNow
            throw new InvalidOperationException(...);

        // UserPrediction.Create also needs the provider passed through
        var predictions = predictedScores
            .Select(p => UserPrediction.Create(userId, p.MatchId, p.HomeScore, p.AwayScore, _dateTimeProvider))
            .ToList();

        return predictions;
    }
}
```

### Step 7: Update command handlers (cascading changes)

Every command handler that calls the modified methods must inject `IDateTimeProvider` and pass it through. The pattern is:

```csharp
public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly IDateTimeProvider _dateTimeProvider;  // NEW

    public CreateLeagueCommandHandler(
        ILeagueRepository leagueRepository,
        IDateTimeProvider dateTimeProvider)  // NEW
    {
        _leagueRepository = leagueRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = League.Create(
            request.SeasonId,
            request.Name,
            request.Price,
            request.EntryDeadlineUtc,
            request.AdministratorUserId,
            _dateTimeProvider);  // NEW — pass through

        // ...
    }
}
```

Grep for all callers of the modified methods and update each one:

```bash
# Find all callers that need updating
grep -rn "League\.Create\|League\.CreateOfficialPublicLeague\|\.AddMember(" src/ --include="*.cs"
grep -rn "UserPrediction\.Create\|\.SetOutcome(" src/ --include="*.cs"
grep -rn "LeagueMember\.Create\|\.Approve(" src/ --include="*.cs"
grep -rn "Round\.UpdateStatus\|\.UpdateLastReminderSent(" src/ --include="*.cs"
grep -rn "Winning\.Create" src/ --include="*.cs"
grep -rn "PasswordResetToken\.Create\|\.IsExpired\b" src/ --include="*.cs"
grep -rn "RefreshToken.*\.IsExpired\|RefreshToken.*\.IsActive\|\.Revoke(" src/ --include="*.cs"
```

### Step 8: Create FakeDateTimeProvider for unit tests

In the test project (requires task 00 completed first), create `tests/Unit/ThePredictions.Domain.Tests.Unit/Helpers/FakeDateTimeProvider.cs`:

```csharp
using PredictionLeague.Domain.Common;

namespace ThePredictions.Domain.Tests.Unit.Helpers;

public class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}
```

Usage in tests:

```csharp
var dateTimeProvider = new FakeDateTimeProvider(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

var prediction = UserPrediction.Create("user-1", 1, 2, 1, dateTimeProvider);

prediction.CreatedAtUtc.Should().Be(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
```

Advancing time within a test:

```csharp
var dateTimeProvider = new FakeDateTimeProvider(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

var token = PasswordResetToken.Create("test-token-abc", "user-1", dateTimeProvider);
token.IsExpired(dateTimeProvider).Should().BeFalse();

// Advance time past expiry
dateTimeProvider.UtcNow = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
token.IsExpired(dateTimeProvider).Should().BeTrue();
```

## Verification

- [ ] No remaining `DateTime.UtcNow` calls in `src/PredictionLeague.Domain/`
- [ ] `IDateTimeProvider` interface created in `Domain/Common/`
- [ ] `DateTimeProvider` implementation created in `Infrastructure/`
- [ ] Registered as singleton in `DependencyInjection.cs`
- [ ] All entity factory methods accept `IDateTimeProvider` as last parameter
- [ ] `PasswordResetToken.IsExpired` and `RefreshToken.IsExpired`/`IsActive` converted from properties to methods
- [ ] `PredictionDomainService` uses constructor injection
- [ ] All command handlers updated to inject and pass through `IDateTimeProvider`
- [ ] `FakeDateTimeProvider` created in the test project helpers folder
- [ ] `dotnet build` succeeds with no errors

## Notes

- `IDateTimeProvider` is placed in `Domain/Common/` because domain entities depend on it — it must live in the domain layer to avoid circular references.
- The production `DateTimeProvider` lives in `Infrastructure/` following the convention that implementations of domain interfaces are in the infrastructure layer.
- Registered as **singleton** because it has no state — it simply wraps `DateTime.UtcNow`.
- `FakeDateTimeProvider` has a **settable** `UtcNow` property so tests can advance time mid-test (useful for token expiry tests).
- No mocking framework is needed for this — `FakeDateTimeProvider` is a simple hand-rolled fake. NSubstitute is not required in the domain unit test project.
- Token `IsExpired`/`IsActive` properties are converted to methods because properties cannot accept parameters. All callers of these members will need updating.
- This task has cascading changes to command handlers in the Application layer. These should be updated as part of this task to keep the solution building.
