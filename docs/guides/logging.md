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
// CORRECT - identifiers only
_logger.LogInformation("Login succeeded for User (ID: {UserId}).", user.Id);
_logger.LogInformation("Login failed for User (ID: {UserId}): incorrect password.", user.Id);

// When no account matched, there is no identifier to log - say so without echoing the input
_logger.LogInformation("Login failed: no account exists for the supplied email address.");
```

## Never Log Personal Data

**Log identifiers, never names, email addresses or phone numbers.** Logs are shipped to
Datadog, a third-party processor, and personal data there is outside the reach of the
anonymisation and verification the database tooling applies.

```csharp
// WRONG - sends personal data to a third party
_logger.LogInformation("Sent digest to {Email}", user.Email);
_logger.LogWarning("Failed login for {Email}", request.Email);

// CORRECT
_logger.LogInformation("Sent digest to User (ID: {UserId})", user.Id);
```

A `UserId` is enough to identify the record when investigating; the email address adds
nothing you cannot look up. If a failed operation has no id available - a login attempt
for an address with no account, for instance - log the fact without echoing the input.

## What Is Logged Automatically

`LoggingBehaviour` sits in the MediatR pipeline and records the name, outcome and duration
of **every command**, so a handler does not need to log that it ran:

```text
CreateLeagueCommand completed in 84ms
SubmitPredictionsCommand failed after 12ms (ValidationException)
```

- **Queries are skipped.** They are high-frequency reads, and reads slow enough to matter
  are already reported by `DapperReadDbConnection` at Warning.
- It is registered **before** `ValidationBehaviour` in `API/DependencyInjection.cs`.
  Registration order is execution order, so commands rejected by validation are recorded
  too - they would otherwise leave no trace at all.

Add an explicit log on top of this only when the command name alone does not carry the
answer you would want later: authentication outcomes, payment and fulfilment steps, and
anything where "did this actually happen for this user" is a question you expect to be
asked.

## Choosing a Level

| Level | Use for | Reaches Datadog |
|-------|---------|-----------------|
| `Debug` | Local diagnosis only | No |
| `Information` | What happened: commands, auth outcomes, payments, scheduled work | Yes |
| `Warning` | Something needs a human to look: slow queries, degraded dependencies | Yes, **and alerts** |
| `Error` | An unhandled fault | Yes, **and alerts** |

Two rules follow from Warning and Error being wired to Slack alert channels:

**A rejected command is `Information`, not `Warning`.** Failed validation or a passed
deadline is usually the user's mistake and needs no action from us. Logging it higher
fills the alerts channel with noise and trains everyone to ignore it.

**Only raise to `Warning` if you would want to be interrupted.** `Warning` posts to
`#alerts-warnings` and `Error` to `#alerts-errors`, both grouped per environment. If a
message does not warrant investigation, it belongs at `Information`.

Serilog's minimum level is `Warning` globally with `ThePredictions` overridden to
`Information` (`appsettings.json`), so our own code logs at Information while third-party
libraries stay quiet.
