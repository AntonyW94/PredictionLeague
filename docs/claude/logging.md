# Logging Guidelines

This project uses Serilog with Datadog integration. Follow these conventions for consistent, searchable logs.

## Entity ID Format

**ALWAYS use the format `EntityName (ID: {EntityNameId})` when logging entity references.**

This format provides:
- Clear identification of the entity type
- Structured logging with named placeholders
- Easy searching in log aggregation tools

```csharp
// CORRECT
_logger.LogInformation("Processing Round (ID: {RoundId})", round.Id);
_logger.LogInformation("User (ID: {UserId}) joined League (ID: {LeagueId})", userId, leagueId);
_logger.LogWarning("Match (ID: {MatchId}) not found", matchId);
_logger.LogError("Failed to update League (ID: {LeagueId}) for User (ID: {UserId})", leagueId, userId);

// WRONG - missing "ID:" label
_logger.LogInformation("Processing Round {RoundId}", round.Id);
_logger.LogInformation("User {UserId} joined League {LeagueId}", userId, leagueId);

// WRONG - inconsistent format
_logger.LogInformation("Processing round with id {RoundId}", round.Id);
_logger.LogInformation("User ID: {UserId} joined league ID: {LeagueId}", userId, leagueId);
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
_logger.LogWarning("League (ID: {LeagueId}) has no active members, skipping prize distribution",
    leagueId);

// Error - failures
_logger.LogError(ex, "Failed to send reminder email to User (ID: {UserId})", userId);
```

## Structured Logging

Use named placeholders for all variable data. Never use string interpolation.

```csharp
// CORRECT - structured logging
_logger.LogInformation("Created League (ID: {LeagueId}) with name {LeagueName}",
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
    _logger.LogDebug("Creating League with name {LeagueName} for User (ID: {UserId})",
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
    _logger.LogWarning("League (ID: {LeagueId}) has no members, cannot calculate standings",
        leagueId);
    return Enumerable.Empty<StandingDto>();
}
```

### Batch Operations

```csharp
_logger.LogInformation("Processing {MatchCount} matches for Round (ID: {RoundId})",
    matches.Count, roundId);

foreach (var match in matches)
{
    _logger.LogDebug("Updating score for Match (ID: {MatchId})", match.Id);
    // ... update logic
}

_logger.LogInformation("Completed processing Round (ID: {RoundId})", roundId);
```
