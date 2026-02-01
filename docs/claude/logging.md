# Logging Guidelines

This project uses Serilog with Datadog integration. Follow these conventions for consistent, searchable logs.

## Variable Formatting

**ALWAYS use the format `Subject (Label: {Placeholder})` when logging any variable.**

This format provides:
- Clear context about what the variable represents
- Structured logging with named placeholders
- Easy searching in log aggregation tools

### Format Pattern

```
Subject (Label: {PlaceholderName})
```

- **Subject** - What you're referring to (User, League, Email, File, etc.)
- **Label** - What aspect of the subject (ID, Name, Email, Path, Count, etc.)
- **Placeholder** - The structured logging placeholder in PascalCase

### Examples by Type

#### Entity IDs (most common)

```csharp
// CORRECT
_logger.LogInformation("Processing Round (ID: {RoundId})", round.Id);
_logger.LogInformation("User (ID: {UserId}) joined League (ID: {LeagueId})", userId, leagueId);
_logger.LogWarning("Match (ID: {MatchId}) not found", matchId);

// WRONG - missing label
_logger.LogInformation("Processing Round {RoundId}", round.Id);
_logger.LogInformation("User {UserId} joined League {LeagueId}", userId, leagueId);
```

#### Names and Strings

```csharp
// CORRECT
_logger.LogInformation("User (Name: {UserName}) logged in", user.Name);
_logger.LogInformation("Creating League (Name: {LeagueName})", request.Name);
_logger.LogWarning("Team (Name: {TeamName}) not found in competition", teamName);

// WRONG - no context
_logger.LogInformation("Creating {LeagueName}", request.Name);
```

#### Email Addresses

```csharp
// CORRECT
_logger.LogInformation("Sending welcome email to User (Email: {UserEmail})", user.Email);
_logger.LogWarning("Invalid email format for User (Email: {Email})", email);

// WRONG
_logger.LogInformation("Sending welcome email to {Email}", user.Email);
```

#### Counts and Numbers

```csharp
// CORRECT
_logger.LogInformation("Processing Matches (Count: {MatchCount}) for Round (ID: {RoundId})",
    matches.Count, roundId);
_logger.LogDebug("User (ID: {UserId}) has Points (Total: {TotalPoints})", userId, points);
_logger.LogInformation("Retry attempt (Number: {AttemptNumber}) of (Max: {MaxAttempts})",
    attempt, maxAttempts);

// WRONG
_logger.LogInformation("Processing {MatchCount} matches for Round {RoundId}",
    matches.Count, roundId);
```

#### Codes and Identifiers

```csharp
// CORRECT
_logger.LogInformation("User joined League (Code: {EntryCode})", entryCode);
_logger.LogDebug("Processing request (CorrelationId: {CorrelationId})", correlationId);
_logger.LogInformation("Season (Year: {SeasonYear}) started", season.Year);

// WRONG
_logger.LogInformation("User joined league with code {EntryCode}", entryCode);
```

#### File Paths and URLs

```csharp
// CORRECT
_logger.LogInformation("Reading configuration from File (Path: {FilePath})", path);
_logger.LogDebug("Calling external API (URL: {ApiUrl})", url);

// WRONG
_logger.LogInformation("Reading configuration from {Path}", path);
```

#### Status and State

```csharp
// CORRECT
_logger.LogInformation("Round (ID: {RoundId}) changed to Status (Value: {RoundStatus})",
    roundId, newStatus);
_logger.LogDebug("Prediction (ID: {PredictionId}) has State (Current: {State})",
    predictionId, state);

// WRONG
_logger.LogInformation("Round {RoundId} status is now {Status}", roundId, newStatus);
```

### Multiple Variables

When logging multiple variables, each gets its own labelled format:

```csharp
// CORRECT - each variable is clearly labelled
_logger.LogInformation(
    "User (ID: {UserId}) created League (Name: {LeagueName}) for Season (ID: {SeasonId})",
    userId, leagueName, seasonId);

_logger.LogWarning(
    "Failed to send Email (Type: {EmailType}) to User (Email: {UserEmail}) after Attempts (Count: {AttemptCount})",
    emailType, userEmail, attemptCount);

// WRONG - mixed formatting
_logger.LogInformation(
    "User {UserId} created league {LeagueName} for Season (ID: {SeasonId})",
    userId, leagueName, seasonId);
```

## Log Levels

Use appropriate log levels:

| Level | Use For | Example |
|-------|---------|---------|
| `Trace` | Detailed debugging info | Variable values during loops |
| `Debug` | Development debugging | Method entry/exit, state changes |
| `Information` | Normal operations | User actions, successful operations |
| `Warning` | Unexpected but handled | Retries, fallbacks, missing optional data |
| `Error` | Failures requiring attention | Exceptions, failed operations |
| `Critical` | System-level failures | Database down, critical service unavailable |

```csharp
// Information - normal operations
_logger.LogInformation("User (ID: {UserId}) submitted predictions for Round (ID: {RoundId})",
    userId, roundId);

// Warning - unexpected but handled
_logger.LogWarning("League (ID: {LeagueId}) has Members (Count: {MemberCount}), skipping prize distribution",
    leagueId, 0);

// Error - failures
_logger.LogError(ex, "Failed to send reminder email to User (Email: {UserEmail})", userEmail);
```

## Structured Logging

Use named placeholders for all variable data. Never use string interpolation.

```csharp
// CORRECT - structured logging
_logger.LogInformation("Created League (ID: {LeagueId}) with Name (Value: {LeagueName})",
    league.Id, league.Name);

// WRONG - string interpolation (loses structure)
_logger.LogInformation($"Created League (ID: {league.Id}) with name {league.Name}");

// WRONG - concatenation
_logger.LogInformation("Created League (ID: " + league.Id + ") with name " + league.Name);
```

## Exception Logging

Always pass the exception as the first parameter when logging errors:

```csharp
try
{
    await _repository.CreateAsync(entity, ct);
}
catch (Exception ex)
{
    // CORRECT - exception as first parameter
    _logger.LogError(ex, "Failed to create League (ID: {LeagueId})", league.Id);
    throw;
}

// WRONG - exception not passed
_logger.LogError("Failed to create League: {ErrorMessage}", ex.Message);
```

## Common Logging Patterns

### Operation Start/End

```csharp
public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken ct)
{
    _logger.LogDebug("Creating League (Name: {LeagueName}) for User (ID: {UserId})",
        request.Name, request.UserId);

    var league = await _leagueRepository.CreateAsync(entity, ct);

    _logger.LogInformation("League (ID: {LeagueId}) created successfully", league.Id);

    return new LeagueDto(league.Id, league.Name);
}
```

### Conditional Logging

```csharp
if (members.Count == 0)
{
    _logger.LogWarning("League (ID: {LeagueId}) has Members (Count: {MemberCount}), cannot calculate standings",
        leagueId, members.Count);
    return Enumerable.Empty<StandingDto>();
}
```

### Batch Operations

```csharp
_logger.LogInformation("Processing Matches (Count: {MatchCount}) for Round (ID: {RoundId})",
    matches.Count, roundId);

foreach (var match in matches)
{
    _logger.LogDebug("Updating score for Match (ID: {MatchId})", match.Id);
    // ... update logic
}

_logger.LogInformation("Completed processing Round (ID: {RoundId})", roundId);
```

### Authentication Events

```csharp
_logger.LogInformation("User (Email: {UserEmail}) logged in from IP (Address: {IpAddress})",
    email, ipAddress);

_logger.LogWarning("Failed login attempt for User (Email: {UserEmail}) from IP (Address: {IpAddress})",
    email, ipAddress);
```
