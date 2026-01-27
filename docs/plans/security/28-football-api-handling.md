# P2: Football API Response Handling

## Summary

**Severity:** P2 - Medium
**Type:** External Data Handling / Error Handling
**CWE:** CWE-754 (Improper Check for Unusual or Exceptional Conditions)

## Description

Football API responses are deserialized without explicit validation, and there are potential null reference issues when parsing data. If the external API changes its response structure or returns unexpected data, the application could fail silently or throw unhandled exceptions.

## Affected Files

- `PredictionLeague.Infrastructure/Services/FootballDataService.cs` (lines 25, 32, 44, 51, 58)
- `PredictionLeague.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs` (lines 60, 88, 117)

## Issues Identified

### 1. Loose Deserialization

```csharp
// FootballDataService.cs
var wrapper = await _httpClient.GetFromJsonAsync<FixtureResponseWrapper>(endpoint, cancellationToken);
return wrapper?.Response ?? Enumerable.Empty<FixtureResponse>();
```

If the API response structure changes, deserialization silently returns null/empty.

### 2. Null Reference Risk

```csharp
// SyncSeasonWithApiCommandHandler.cs
var earliestMatchDate = currentRoundFixtures.Min(f => f.Fixture?.Date);  // Line 54
if (earliestMatchDate == null) continue;

var fixtureDateUtc = rescheduledMatch.Fixture.Date.UtcDateTime;  // Line 88 - assumes not null
```

After checking `earliestMatchDate`, the code later accesses `Fixture.Date` without null check.

### 3. External API Error Details Exposed

```csharp
catch (HttpRequestException ex)
{
    throw new ValidationException($"...Details: {ex.Message}");  // Exposes internal details
}
```

## Exploitation Scenario

1. External Football API is modified or has an outage
2. Deserialization returns null/empty data
3. Season sync fails silently or throws NullReferenceException
4. Application enters inconsistent state with missing/incomplete data

## Recommended Fixes

### 1. Add Response Validation

```csharp
// FootballDataService.cs
public async Task<IEnumerable<FixtureResponse>> GetFixturesAsync(
    int leagueId, int seasonYear, CancellationToken cancellationToken)
{
    var endpoint = $"fixtures?league={leagueId}&season={seasonYear}";
    var wrapper = await _httpClient.GetFromJsonAsync<FixtureResponseWrapper>(endpoint, cancellationToken);

    // Validate response
    if (wrapper == null)
    {
        _logger.LogWarning("Football API returned null response for league {LeagueId}, season {SeasonYear}",
            leagueId, seasonYear);
        throw new ExternalServiceException("Football API returned invalid response");
    }

    if (wrapper.Errors?.Any() == true)
    {
        _logger.LogError("Football API errors: {Errors}", wrapper.Errors);
        throw new ExternalServiceException("Football API returned errors");
    }

    return wrapper.Response ?? Enumerable.Empty<FixtureResponse>();
}
```

### 2. Fix Null Reference Issues

```csharp
// SyncSeasonWithApiCommandHandler.cs
var rescheduledFixtures = roundFixtures
    .Where(f => f.Fixture?.Date != null)  // Ensure not null
    .ToList();

foreach (var rescheduledMatch in rescheduledFixtures)
{
    // Now safe to access
    var fixtureDateUtc = rescheduledMatch.Fixture!.Date!.UtcDateTime;
}
```

### 3. Sanitise Error Messages

```csharp
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Football API request failed for league {LeagueId}", request.LeagueId);
    throw new ValidationException("Could not retrieve data from the football API. Please try again later.");
}
```

### 4. Add Circuit Breaker (Optional Enhancement)

```csharp
// Consider adding Polly for resilience
services.AddHttpClient<IFootballDataService, FootballDataService>()
    .AddPolicyHandler(GetCircuitBreakerPolicy());

private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));
}
```

## Testing

1. Mock Football API to return malformed JSON
2. Verify application handles gracefully with appropriate error messages
3. Mock Football API timeout
4. Verify null values in fixture data are handled
5. Check logs contain useful debugging information (without sensitive data)

## References

- [OWASP External Service Interaction](https://owasp.org/www-community/vulnerabilities/External_Service_Interaction)
- [Polly Resilience Library](https://github.com/App-vNext/Polly)
