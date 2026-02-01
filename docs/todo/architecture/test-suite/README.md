# PredictionLeague Test Suite Plan

## Status

**Not Started** | In Progress | Complete

This document outlines the comprehensive testing strategy for the PredictionLeague application, including CI/CD integration with GitHub Actions.

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Testing Stack](#2-testing-stack)
3. [Test Naming Convention](#3-test-naming-convention)
4. [What Can Be Tested](#4-what-can-be-tested)
5. [What Cannot (Easily) Be Tested](#5-what-cannot-easily-be-tested)
6. [SQLite for Integration Testing](#6-sqlite-for-integration-testing)
7. [WebApplicationFactory for API Testing](#7-webapplicationfactory-for-api-testing)
8. [Playwright for End-to-End Testing](#8-playwright-for-end-to-end-testing)
9. [Catching SQL and Dapper Mapping Errors](#9-catching-sql-and-dapper-mapping-errors)
10. [Development Database Strategy](#10-development-database-strategy)
11. [GitHub Actions Integration](#11-github-actions-integration)
12. [Test Project Structure](#12-test-project-structure)
13. [Implementation Priority](#13-implementation-priority)
14. [Package References](#14-package-references)

---

## 1. Executive Summary

The codebase is **well-structured for testing**. The Clean Architecture with CQRS provides natural seams for unit testing, and the rich domain models contain testable business logic.

| Test Type | Feasibility | Approach |
|-----------|-------------|----------|
| Domain Entity Unit Tests | ✅ Excellent | Pure unit tests, no mocks needed |
| Domain Service Unit Tests | ✅ Excellent | Pure unit tests |
| Validator Unit Tests | ✅ Excellent | Pure unit tests |
| Command Handler Unit Tests | ✅ Good | Mock repositories with NSubstitute |
| Query Handler Integration Tests | ✅ Good | SQLite in-memory database |
| Repository Integration Tests | ✅ Good | SQLite in-memory database |
| API Integration Tests | ✅ Good | WebApplicationFactory with SQLite |
| E2E Tests | ✅ Good | Playwright on GitHub Actions |
| Blazor Component Tests | ⚠️ Moderate | bUnit library (optional) |

### Cost Summary

All testing infrastructure runs on **GitHub Actions free tier**:
- 2,000 minutes/month for private repositories
- Estimated usage: ~1,200-1,400 minutes/month
- No Docker required
- No additional infrastructure costs

---

## 2. Testing Stack

| Purpose | Tool | Version |
|---------|------|---------|
| Test Framework | xUnit.v3 | 3.x |
| Assertions | FluentAssertions | 7.x |
| Mocking | NSubstitute | 5.x |
| Integration DB | SQLite In-Memory | Microsoft.Data.Sqlite 8.x |
| API Testing | WebApplicationFactory | Built into ASP.NET Core 8.x |
| E2E Testing | Playwright | 1.x |
| Test Data Generation | Bogus | 35.x |

---

## 3. Test Naming Convention

All tests follow the pattern:

```
{Method}_Should{ExpectedBehaviour}_When{Conditions}
```

### Examples

```csharp
// Domain entity tests
SetOutcome_ShouldReturnExactScore_WhenPredictionMatchesActualScore()
SetOutcome_ShouldReturnCorrectResult_WhenPredictionMatchesWinner()
SetOutcome_ShouldReturnIncorrect_WhenPredictionDoesNotMatch()

// Factory method tests
Create_ShouldGenerateEntryCode_WhenValidParametersProvided()
Create_ShouldThrowArgumentException_WhenNameIsEmpty()
Create_ShouldThrowArgumentException_WhenNameExceeds100Characters()

// Handler tests
Handle_ShouldCreateLeague_WhenRequestIsValid()
Handle_ShouldReturnNull_WhenUserIsNotMember()

// Validator tests
Validate_ShouldFail_WhenNameIsEmpty()
Validate_ShouldPass_WhenAllFieldsAreValid()

// Repository tests
GetByIdAsync_ShouldReturnLeague_WhenLeagueExists()
GetByIdAsync_ShouldReturnNull_WhenLeagueDoesNotExist()
```

---

## 4. What Can Be Tested

### 4.1 Domain Layer (Highest ROI - Start Here)

Pure unit tests with no mocks required:

#### League Entity Methods
| Method | Test Scenarios |
|--------|----------------|
| `Create()` | Valid input, empty name, name too long, invalid season |
| `GenerateEntryCode()` | Length is 6, alphanumeric only |
| `UpdateDetails()` | Valid update, invalid name |
| `AddMember()` / `RemoveMember()` | Success, after deadline, duplicate member |
| `GetRoundWinners()` | Single winner, tied winners, no results |
| `GetPeriodWinners()` | Multi-round aggregation, empty rounds |
| `GetOverallRankings()` | Rankings with ties, empty members |
| `GetMostExactScoresWinners()` | Single winner, tied, no exact scores |

#### UserPrediction Entity
| Method | Test Scenarios |
|--------|----------------|
| `Create()` | Valid input, negative scores |
| `SetOutcome()` | Exact score, correct result (home win, away win, draw), incorrect |

#### BoostEligibilityEvaluator (Static Domain Service)
This is the most complex testable unit with 10+ conditional paths:
- Boost disabled
- Season limit reached
- Window-based usage limits exceeded
- Round validation failures
- Remaining uses calculations

#### Round Entity
| Method | Test Scenarios |
|--------|----------------|
| `Create()` | Valid dates, deadline after start date error |
| `UpdateStatus()` | Draft→Published, Published→InProgress, InProgress→Completed, invalid transitions |
| `AddMatch()` / `RemoveMatch()` | Success, duplicate match, team playing itself |

### 4.2 Validators (28 Classes - Quick Wins)

FluentValidation provides `TestValidate()` for easy testing:

```csharp
public class CreateLeagueRequestValidatorTests
{
    private readonly CreateLeagueRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new CreateLeagueRequest { Name = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("ab")]       // Too short
    [InlineData("")]         // Empty
    public void Validate_ShouldFail_WhenNameIsTooShort(string name)
    {
        var request = new CreateLeagueRequest { Name = name };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
```

### 4.3 Command Handlers (67 Handlers)

Unit tests with mocked dependencies:

```csharp
public class CreateLeagueCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly CreateLeagueCommandHandler _handler;

    public CreateLeagueCommandHandlerTests()
    {
        _handler = new CreateLeagueCommandHandler(_leagueRepository, _currentUser);
    }

    [Fact]
    public async Task Handle_ShouldCreateLeagueAndReturnId_WhenRequestIsValid()
    {
        // Arrange
        var command = new CreateLeagueCommand { Name = "Test League", SeasonId = 1 };
        _currentUser.UserId.Returns("user-123");
        _leagueRepository.CreateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>())
            .Returns(new League(id: 42, ...));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(42);
        await _leagueRepository.Received(1).CreateAsync(
            Arg.Is<League>(l => l.Name == "Test League"),
            Arg.Any<CancellationToken>());
    }
}
```

### 4.4 Prize Strategies (4 Implementations)

The strategy pattern makes these highly testable:

```csharp
public class RoundPrizeStrategyTests
{
    [Fact]
    public void Calculate_ShouldDistributePrize_WhenSingleWinner()
    {
        // Test prize distribution logic
    }

    [Fact]
    public void Calculate_ShouldSplitPrize_WhenMultipleWinnersTied()
    {
        // Test tie-breaking/splitting logic
    }
}
```

---

## 5. What Cannot (Easily) Be Tested

### 5.1 External API Calls (api-sports.io)

The `FootballDataService` calls an external API.

**Options:**
- Mock `HttpClient` for unit tests
- Use **WireMock.NET** for integration tests with canned responses
- Don't test actual API calls (they're external and unreliable)

### 5.2 Email Sending (Brevo)

The `BrevoEmailService` sends real emails.

**Options:**
- Mock `IEmailService` interface in tests
- Don't test actual email delivery

### 5.3 Azure Key Vault Integration

Configuration loading from Key Vault cannot be directly tested. Test the code that *uses* the configuration instead.

### 5.4 Scheduled Job Timing

The actual scheduling (cron-job.org) cannot be tested, but the job handlers (`TasksController`) can be tested through API integration tests.

---

## 6. SQLite for Integration Testing

### 6.1 How It Works

SQLite in-memory creates a temporary database that:
- Lives entirely in RAM
- Is created fresh for each test (or test class)
- Requires no external processes or containers
- Works directly in Visual Studio without any setup

**No Docker required.** Just run tests like any other unit test.

### 6.2 Visual Studio Experience

1. Open Test Explorer (Test → Test Explorer)
2. Click "Run All Tests" or right-click specific tests
3. Tests run immediately - no containers to start
4. Each test gets a fresh database instance

### 6.3 SQL Server vs SQLite Compatibility

SQLite is **not 100% compatible** with SQL Server. Here are the key differences and workarounds:

#### MERGE Statements (Not Supported in SQLite)

The codebase uses MERGE in 4 repositories:
- `LeagueRepository.UpdateLeagueRoundResultsAsync()`
- `LeagueRepository.UpdateLeagueRoundBoostsAsync()`
- `UserPredictionRepository.UpsertBatchAsync()`
- `RoundRepository` and `SeasonRepository` (similar patterns)

**Solution: SQL Dialect Abstraction**

Create an interface that provides database-specific SQL:

```csharp
public interface ISqlDialect
{
    string UpsertPredictionSql { get; }
    string UpdateLeagueRoundResultsSql { get; }
    string GetIdentitySql { get; }
    string GetUtcDateSql { get; }
}

public class SqlServerDialect : ISqlDialect
{
    public string UpsertPredictionSql => @"
        MERGE INTO [UserPredictions] AS target
        USING (SELECT @UserId AS UserId, @MatchId AS MatchId) AS source
        ON (target.[UserId] = source.[UserId] AND target.[MatchId] = source.[MatchId])
        WHEN MATCHED THEN
            UPDATE SET [PredictedHomeScore] = @PredictedHomeScore, ...
        WHEN NOT MATCHED THEN
            INSERT (...) VALUES (...);";

    public string GetIdentitySql => "SELECT CAST(SCOPE_IDENTITY() as int)";
    public string GetUtcDateSql => "GETUTCDATE()";
}

public class SqliteDialect : ISqlDialect
{
    public string UpsertPredictionSql => @"
        INSERT INTO [UserPredictions] ([MatchId], [UserId], [PredictedHomeScore], ...)
        VALUES (@MatchId, @UserId, @PredictedHomeScore, ...)
        ON CONFLICT([UserId], [MatchId]) DO UPDATE SET
            [PredictedHomeScore] = excluded.[PredictedHomeScore],
            [PredictedAwayScore] = excluded.[PredictedAwayScore],
            [UpdatedAtUtc] = excluded.[UpdatedAtUtc];";

    public string GetIdentitySql => "SELECT last_insert_rowid()";
    public string GetUtcDateSql => "datetime('now')";
}
```

**Alternative: Test at a Higher Level**

Instead of testing repositories directly with SQLite, test at the API level where the full SQL Server-compatible queries run. Use SQLite only for simpler read queries.

#### Other SQL Server Features Not in SQLite

| SQL Server | SQLite Equivalent |
|------------|-------------------|
| `SCOPE_IDENTITY()` | `last_insert_rowid()` |
| `GETUTCDATE()` | `datetime('now')` |
| `MERGE` | `INSERT ... ON CONFLICT DO UPDATE` |
| `[Brackets]` | Works in SQLite ✅ |
| `TOP N` | `LIMIT N` |
| `ISNULL()` | `IFNULL()` or `COALESCE()` |

### 6.4 SQLite Test Fixture Implementation

```csharp
using Microsoft.Data.Sqlite;
using System.Data;

public class SqliteTestFixture : IDisposable
{
    public SqliteConnection Connection { get; }

    public SqliteTestFixture()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
        InitialiseSchema();
    }

    private void InitialiseSchema()
    {
        const string sql = @"
            CREATE TABLE [Leagues] (
                [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [Name] TEXT NOT NULL,
                [SeasonId] INTEGER NOT NULL,
                [Price] REAL NOT NULL,
                [AdministratorUserId] TEXT NOT NULL,
                [EntryCode] TEXT NOT NULL,
                [CreatedAtUtc] TEXT NOT NULL,
                [EntryDeadlineUtc] TEXT NOT NULL,
                [PointsForExactScore] INTEGER NOT NULL,
                [PointsForCorrectResult] INTEGER NOT NULL,
                [IsFree] INTEGER NOT NULL,
                [HasPrizes] INTEGER NOT NULL,
                [PrizeFundOverride] REAL
            );

            CREATE TABLE [LeagueMembers] (
                [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [LeagueId] INTEGER NOT NULL,
                [UserId] TEXT NOT NULL,
                [Status] TEXT NOT NULL,
                [IsAlertDismissed] INTEGER NOT NULL DEFAULT 0,
                [JoinedAtUtc] TEXT NOT NULL,
                [ApprovedAtUtc] TEXT,
                FOREIGN KEY ([LeagueId]) REFERENCES [Leagues]([Id])
            );

            CREATE TABLE [Rounds] (
                [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [SeasonId] INTEGER NOT NULL,
                [RoundNumber] INTEGER NOT NULL,
                [ApiRoundName] TEXT,
                [StartDateUtc] TEXT NOT NULL,
                [DeadlineUtc] TEXT NOT NULL,
                [Status] TEXT NOT NULL
            );

            CREATE TABLE [Matches] (
                [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [RoundId] INTEGER NOT NULL,
                [HomeTeamId] INTEGER NOT NULL,
                [AwayTeamId] INTEGER NOT NULL,
                [HomeScore] INTEGER,
                [AwayScore] INTEGER,
                [KickoffTimeUtc] TEXT NOT NULL,
                FOREIGN KEY ([RoundId]) REFERENCES [Rounds]([Id])
            );

            CREATE TABLE [UserPredictions] (
                [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [MatchId] INTEGER NOT NULL,
                [UserId] TEXT NOT NULL,
                [PredictedHomeScore] INTEGER NOT NULL,
                [PredictedAwayScore] INTEGER NOT NULL,
                [CreatedAtUtc] TEXT NOT NULL,
                [UpdatedAtUtc] TEXT NOT NULL,
                [Outcome] TEXT,
                UNIQUE([UserId], [MatchId]),
                FOREIGN KEY ([MatchId]) REFERENCES [Matches]([Id])
            );

            -- Add more tables as needed...
        ";

        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Connection.Close();
        Connection.Dispose();
    }
}
```

### 6.5 Recommendation for MERGE Statements

Given the complexity of abstracting SQL dialects, consider this pragmatic approach:

1. **Unit test domain logic** - No database needed
2. **Unit test command handlers** - Mock repositories
3. **Integration test queries** - Use SQLite for read-only queries (most query handlers)
4. **Integration test MERGE operations** - Test at API level or use the dev SQL Server database

Most **query handlers** use standard SELECT statements that work perfectly with SQLite. The MERGE statements are only in 4 repository methods used by **command handlers**, which can be unit tested with mocked repositories.

---

## 7. WebApplicationFactory for API Testing

### 7.1 How It Works

`WebApplicationFactory<TEntryPoint>` is built into ASP.NET Core and creates an **in-process test server**:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Test Process                                  │
│  ┌─────────────┐        ┌─────────────────────────────────┐     │
│  │   Test      │  HTTP  │  WebApplicationFactory          │     │
│  │   Code      │───────▶│  ┌─────────────────────┐        │     │
│  │             │        │  │   Your API          │        │     │
│  │             │◀───────│  │   (Full Pipeline)   │        │     │
│  └─────────────┘        │  │   - Middleware      │        │     │
│                         │  │   - Controllers     │        │     │
│                         │  │   - MediatR         │        │     │
│                         │  │   - Validation      │        │     │
│                         │  │   - Auth            │        │     │
│                         │  └─────────────────────┘        │     │
│                         └─────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────────┘
```

**Key Points:**
- Runs entirely in-process (no network calls)
- Full HTTP pipeline including middleware, routing, auth
- Can override services (swap real DB for SQLite)
- Very fast - no process startup overhead
- Works in Visual Studio Test Explorer like any unit test
- **No Docker required**

### 7.2 Custom Factory Implementation

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

public class PredictionLeagueApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real database connection factory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbConnectionFactory));
            if (descriptor != null)
                services.Remove(descriptor);

            // Create SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            InitialiseDatabase(_connection);

            services.AddSingleton<IDbConnectionFactory>(
                new SqliteConnectionFactory(_connection));
        });

        builder.UseEnvironment("Testing");
    }

    private static void InitialiseDatabase(SqliteConnection connection)
    {
        const string schema = @"
            CREATE TABLE [Leagues] (...);
            CREATE TABLE [LeagueMembers] (...);
            -- etc.
        ";

        using var command = connection.CreateCommand();
        command.CommandText = schema;
        command.ExecuteNonQuery();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}
```

### 7.3 Writing API Integration Tests

```csharp
public class LeaguesApiTests : IClassFixture<PredictionLeagueApiFactory>
{
    private readonly HttpClient _client;
    private readonly PredictionLeagueApiFactory _factory;

    public LeaguesApiTests(PredictionLeagueApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeague_ShouldReturnNotFound_WhenLeagueDoesNotExist()
    {
        await AuthenticateAsync();
        var response = await _client.GetAsync("/api/leagues/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLeague_ShouldReturnCreated_WhenRequestIsValid()
    {
        await AuthenticateAsync();
        var request = new CreateLeagueRequest
        {
            Name = "Test League",
            SeasonId = 1,
            Price = 10.00m
        };

        var response = await _client.PostAsJsonAsync("/api/leagues", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    private async Task AuthenticateAsync()
    {
        // Generate test JWT token or call login endpoint
        var token = GenerateTestJwtToken("user-123", "User");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
```

---

## 8. Playwright for End-to-End Testing

### 8.1 What Is Playwright?

Playwright is a browser automation framework that:
- Controls real browsers (Chromium, Firefox, WebKit)
- Simulates real user interactions
- Tests the full stack including Blazor UI
- Can take screenshots and videos on failure
- Runs on GitHub Actions for free

### 8.2 How It Works on GitHub Actions

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    Playwright E2E Tests on GitHub Actions                    │
│                                                                              │
│  Step 1: Build application                                         (~2 min) │
│  Step 2: Create SQLite test database + seed data                   (~1 min) │
│  Step 3: Install Playwright browsers (cached)                      (~1 min) │
│  Step 4: Start app on localhost:5000                              (~30 sec) │
│  Step 5: Run Playwright tests against localhost                  (~5-8 min) │
│  Step 6: Upload screenshots/videos if tests fail                  (~30 sec) │
│                                                                              │
│  Total: ~10-12 minutes                                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 8.3 Example Playwright Test

```csharp
public class LoginAndNavigationTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private readonly E2ETestSettings _settings;

    public LoginAndNavigationTests()
    {
        _settings = TestConfiguration.GetSettings();
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }

    [Fact]
    public async Task Login_ShouldRedirectToDashboard_WhenCredentialsAreValid()
    {
        await _page.GotoAsync($"{_settings.BaseUrl}/login");

        await _page.FillAsync("input[type='email']", _settings.TestUser.Email);
        await _page.FillAsync("input[type='password']", _settings.TestUser.Password);
        await _page.ClickAsync("button[type='submit']");

        await _page.WaitForURLAsync("**/dashboard");
        await Expect(_page.Locator("h1")).ToContainTextAsync("Dashboard");
    }

    [Fact]
    public async Task Predictions_ShouldSubmitSuccessfully_WhenValidScoresEntered()
    {
        await LoginAsTestUserAsync();
        await _page.GotoAsync($"{_settings.BaseUrl}/leagues/1/predictions");

        await _page.FillAsync("[data-testid='home-score-1']", "2");
        await _page.FillAsync("[data-testid='away-score-1']", "1");
        await _page.ClickAsync("[data-testid='submit-predictions']");

        await Expect(_page.Locator(".success-message"))
            .ToContainTextAsync("Predictions saved");
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}
```

### 8.4 Test Configuration

```csharp
public static class TestConfiguration
{
    public static E2ETestSettings GetSettings()
    {
        return new E2ETestSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")
                ?? "http://localhost:5000",
            TestUser = new TestUserCredentials
            {
                Email = Environment.GetEnvironmentVariable("E2E_TEST_USER_EMAIL")
                    ?? "testplayer@dev.local",
                Password = Environment.GetEnvironmentVariable("E2E_TEST_USER_PASSWORD")
                    ?? "TestPassword123!"
            },
            AdminUser = new TestUserCredentials
            {
                Email = Environment.GetEnvironmentVariable("E2E_ADMIN_EMAIL")
                    ?? "testadmin@dev.local",
                Password = Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD")
                    ?? "TestPassword123!"
            }
        };
    }
}
```

---

## 9. Catching SQL and Dapper Mapping Errors

This section addresses common pain points:
- Parameter order not matching the model
- Columns missing from SELECT breaking constructors

### 9.1 The Problem

Dapper maps SQL results to objects by:
1. **Constructor matching** - Finds a constructor whose parameters match column names
2. **Property mapping** - Maps remaining columns to properties

Common failures:
```csharp
// This query will FAIL at runtime if [ExactScoreCount] is missing
const string sql = "SELECT [LeagueId], [RoundId], [UserId] FROM [LeagueRoundResults]";
var results = await connection.QueryAsync<LeagueRoundResult>(sql);
// Error: No matching constructor found

// This will FAIL if parameters are in wrong order
const string sql = "INSERT INTO [Leagues] ([Name], [SeasonId]) VALUES (@SeasonId, @Name)";
// Data goes into wrong columns!
```

### 9.2 Which Tests Catch These Errors?

| Test Type | Catches Missing Columns? | Catches Wrong Parameter Order? | Effort |
|-----------|--------------------------|-------------------------------|--------|
| **Repository Integration Tests** | ✅ Yes | ✅ Yes | Medium |
| **Query Handler Integration Tests** | ✅ Yes | N/A (queries) | Medium |
| **API Integration Tests** | ✅ Yes | ✅ Yes | Medium-High |
| Unit Tests (mocked) | ❌ No | ❌ No | Low |

**You need integration tests** to catch these errors. Unit tests with mocked repositories won't help because they never execute real SQL.

### 9.3 Recommended Testing Strategy

```
┌─────────────────────────────────────────────────────────────────┐
│                     Testing Strategy                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Priority 1: Query Handler Integration Tests                     │
│  ─────────────────────────────────────────────                   │
│  • Test all query handlers with SQLite                           │
│  • Catches: missing columns, wrong column names, type mismatches │
│  • These are the most common bugs                                │
│                                                                  │
│  Priority 2: Repository Integration Tests                        │
│  ─────────────────────────────────────────                       │
│  • Test all repository methods with SQLite                       │
│  • Catches: INSERT parameter order, missing columns in SELECT    │
│  • Note: MERGE statements need special handling                  │
│                                                                  │
│  Priority 3: API Integration Tests                               │
│  ─────────────────────────────────                               │
│  • Test full request/response cycle                              │
│  • Catches: all of the above plus middleware/validation issues   │
│  • Highest confidence, but slower                                │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. Development Database Strategy

### 10.1 Database Purposes

| Purpose | Database Type | Location |
|---------|---------------|----------|
| **Production** | SQL Server | Fasthosts |
| **Development** | SQL Server | Fasthosts (separate DB) |
| **Automated Tests (CI)** | SQLite In-Memory | GitHub Actions runner |
| **E2E Tests** | SQLite File | GitHub Actions runner |

### 10.2 Dev Database Refresh Strategy

Since Fasthosts shared hosting doesn't support traditional backup/restore, use an **application-level data copy tool**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     Weekly Refresh Process (GitHub Actions)                  │
│                                                                              │
│  1. Connect to Production DB (read-only)                                     │
│  2. Read all tables in dependency order                                      │
│  3. Anonymise PII in memory (emails, names, league names)                    │
│  4. Truncate Dev DB tables                                                   │
│  5. Insert anonymised data to Dev DB                                         │
│  6. Add known test accounts (testplayer@dev.local, testadmin@dev.local)      │
│  7. Verify no real PII remains                                               │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 10.3 Data Anonymisation

The refresh tool anonymises:

| Data Type | Original | Anonymised |
|-----------|----------|------------|
| Emails | john.smith@gmail.com | user123@testmail.com |
| Names | John Smith | TestUser47 Player |
| League names | Smith Family League | Manchester Predictions |
| Entry codes | ABC123 | XYZ789 (regenerated) |
| Passwords | (hashed) | INVALIDATED |
| Refresh tokens | (deleted) | (deleted) |

### 10.4 Test Accounts

Fixed test accounts with known credentials:

| Account | Email | Password | Role |
|---------|-------|----------|------|
| Test Player | testplayer@dev.local | TestPassword123! | User |
| Test Admin | testadmin@dev.local | TestPassword123! | Admin |

These accounts are:
- Added to the first league automatically
- Available immediately after DB refresh
- Used by Playwright E2E tests
- Used for manual testing

---

## 11. GitHub Actions Integration

All testing and deployment runs on GitHub Actions free tier.

### 11.1 Workflow Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        GitHub Actions Workflows                              │
│                                                                              │
│  ci.yml (On every push/PR)                                                   │
│  ├── Build solution                                                          │
│  ├── Run unit tests (Domain, Validators, Application)                        │
│  ├── Run integration tests (Infrastructure, API)                             │
│  └── ~8-12 minutes                                                           │
│                                                                              │
│  deploy.yml (Manual trigger)                                                 │
│  ├── Build and run tests                                                     │
│  ├── Publish application                                                     │
│  ├── Deploy to Fasthosts via FTP                                             │
│  └── ~5-8 minutes                                                            │
│                                                                              │
│  refresh-dev-db.yml (Weekly Monday 6am + manual)                             │
│  ├── Copy data from prod to dev                                              │
│  ├── Anonymise PII                                                           │
│  ├── Add test accounts                                                       │
│  └── ~5 minutes                                                              │
│                                                                              │
│  e2e.yml (After CI success on main + manual)                                 │
│  ├── Build application                                                       │
│  ├── Setup SQLite test database                                              │
│  ├── Install Playwright                                                      │
│  ├── Start application                                                       │
│  ├── Run Playwright tests                                                    │
│  └── ~10-12 minutes                                                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 11.2 Time Budget

| Workflow | Duration | Frequency | Monthly Minutes |
|----------|----------|-----------|-----------------|
| CI (build + all tests) | ~10 mins | 5/day × 22 days | ~1,100 mins |
| Deploy to FTP | ~6 mins | 4/month | ~24 mins |
| DB Refresh | ~5 mins | 4/month | ~20 mins |
| E2E Tests | ~12 mins | 8/month | ~96 mins |
| **Total** | | | **~1,240 mins** |

**Free tier: 2,000 mins/month** - Well within limits.

### 11.3 Required GitHub Secrets

Configure in: Repository → Settings → Secrets and variables → Actions

| Secret Name | Description |
|-------------|-------------|
| `PROD_CONNECTION_STRING` | Fasthosts production SQL connection string |
| `DEV_CONNECTION_STRING` | Fasthosts dev SQL connection string |
| `TEST_ACCOUNT_PASSWORD` | Password for test accounts |
| `FTP_SERVER` | Fasthosts FTP server address |
| `FTP_USERNAME` | Fasthosts FTP username |
| `FTP_PASSWORD` | Fasthosts FTP password |

---

## 12. Test Project Structure

```
PredictionLeague.sln
├── src/
│   ├── PredictionLeague.Domain/
│   ├── PredictionLeague.Application/
│   ├── PredictionLeague.Infrastructure/
│   ├── PredictionLeague.Validators/
│   ├── PredictionLeague.API/
│   ├── PredictionLeague.Contracts/
│   └── PredictionLeague.Web.Client/
│
├── tests/
│   ├── PredictionLeague.Domain.Tests/
│   │   ├── Models/
│   │   │   ├── LeagueTests.cs
│   │   │   ├── RoundTests.cs
│   │   │   └── UserPredictionTests.cs
│   │   └── Services/
│   │       ├── BoostEligibilityEvaluatorTests.cs
│   │       └── PredictionDomainServiceTests.cs
│   │
│   ├── PredictionLeague.Application.Tests/
│   │   └── Features/
│   │       ├── Leagues/Commands/
│   │       │   ├── CreateLeagueCommandHandlerTests.cs
│   │       │   └── JoinLeagueCommandHandlerTests.cs
│   │       └── Predictions/Commands/
│   │           └── SubmitPredictionsCommandHandlerTests.cs
│   │
│   ├── PredictionLeague.Validators.Tests/
│   │   ├── CreateLeagueRequestValidatorTests.cs
│   │   ├── SubmitPredictionsRequestValidatorTests.cs
│   │   └── ...
│   │
│   ├── PredictionLeague.Infrastructure.Tests/
│   │   ├── Repositories/
│   │   │   ├── LeagueRepositoryTests.cs
│   │   │   └── UserPredictionRepositoryTests.cs
│   │   ├── Queries/
│   │   │   ├── GetLeagueDashboardQueryHandlerTests.cs
│   │   │   └── GetLeaderboardQueryHandlerTests.cs
│   │   └── Services/
│   │       └── BoostServiceTests.cs
│   │
│   ├── PredictionLeague.API.Tests/
│   │   ├── Controllers/
│   │   │   ├── LeaguesControllerTests.cs
│   │   │   ├── PredictionsControllerTests.cs
│   │   │   └── AuthControllerTests.cs
│   │   └── PredictionLeagueApiFactory.cs
│   │
│   ├── PredictionLeague.E2E.Tests/
│   │   ├── LoginTests.cs
│   │   ├── PredictionFlowTests.cs
│   │   ├── LeaderboardTests.cs
│   │   └── TestConfiguration.cs
│   │
│   └── PredictionLeague.Tests.Shared/
│       ├── Fixtures/
│       │   ├── SqliteTestFixture.cs
│       │   └── SqliteConnectionFactory.cs
│       ├── Builders/
│       │   ├── LeagueBuilder.cs
│       │   └── RoundBuilder.cs
│       └── Extensions/
│           └── TestDataExtensions.cs
│
└── tools/
    ├── PredictionLeague.DevDbRefresh/
    │   ├── Program.cs
    │   └── PredictionLeague.DevDbRefresh.csproj
    │
    └── PredictionLeague.TestDbSeeder/
        ├── Program.cs
        └── PredictionLeague.TestDbSeeder.csproj
```

---

## 13. Implementation Priority

### Phase 1: Domain Unit Tests (Start Here)
**Effort: Low | Value: High | Catches SQL bugs: No**

1. `BoostEligibilityEvaluator` - Most complex logic
2. `UserPrediction.SetOutcome()` - Core scoring algorithm
3. `League.GetRoundWinners()` - Winner calculation
4. `League.GetOverallRankings()` - Ranking with ties
5. `Round` state transitions
6. Entity factory methods

### Phase 2: Validator Tests (Quick Wins)
**Effort: Low | Value: Medium | Catches SQL bugs: No**

All 28 validators - straightforward, fast to write.

### Phase 3: Query Handler Integration Tests ⭐
**Effort: Medium | Value: High | Catches SQL bugs: Yes**

This is where you'll catch most SQL mapping bugs.

1. `GetLeagueDashboardQueryHandler`
2. `GetLeaderboardQueryHandler`
3. All other query handlers

### Phase 4: Repository Integration Tests
**Effort: Medium | Value: High | Catches SQL bugs: Yes**

1. `LeagueRepository` (complex, many methods)
2. `UserPredictionRepository`
3. `RoundRepository`

### Phase 5: Command Handler Unit Tests
**Effort: Medium | Value: Medium | Catches SQL bugs: No (mocked)**

Test business logic orchestration with mocked repositories.

### Phase 6: API Integration Tests
**Effort: Medium-High | Value: High | Catches SQL bugs: Yes**

Full HTTP pipeline testing.

### Phase 7: E2E Tests with Playwright
**Effort: High | Value: Medium-High**

Critical user journeys only.

### Phase 8: CI/CD Setup
**Effort: Medium | Value: High**

GitHub Actions workflows for automated testing and deployment.

---

## 14. Package References

### PredictionLeague.Domain.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PredictionLeague.Domain\PredictionLeague.Domain.csproj" />
  </ItemGroup>
</Project>
```

### PredictionLeague.Application.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PredictionLeague.Application\PredictionLeague.Application.csproj" />
  </ItemGroup>
</Project>
```

### PredictionLeague.Infrastructure.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PredictionLeague.Infrastructure\PredictionLeague.Infrastructure.csproj" />
    <ProjectReference Include="..\PredictionLeague.Tests.Shared\PredictionLeague.Tests.Shared.csproj" />
  </ItemGroup>
</Project>
```

### PredictionLeague.API.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PredictionLeague.API\PredictionLeague.API.csproj" />
    <ProjectReference Include="..\PredictionLeague.Tests.Shared\PredictionLeague.Tests.Shared.csproj" />
  </ItemGroup>
</Project>
```

### PredictionLeague.E2E.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Microsoft.Playwright" Version="1.49.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>
</Project>
```

### PredictionLeague.DevDbRefresh.csproj (Tool)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Bogus" Version="35.6.1" />
    <PackageReference Include="Dapper" Version="2.1.66" />
    <PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.2.0" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>
</Project>
```

---

## Summary

| Aspect | Decision |
|--------|----------|
| **Test Framework** | xUnit.v3 |
| **Assertions** | FluentAssertions |
| **Mocking** | NSubstitute |
| **Integration DB** | SQLite In-Memory |
| **API Testing** | WebApplicationFactory |
| **E2E Testing** | Playwright |
| **CI/CD** | GitHub Actions (free tier) |
| **Dev DB Refresh** | Weekly automated via GitHub Actions |
| **Test Naming** | `{Method}_Should{Behaviour}_When{Conditions}` |

**All testing infrastructure is free and runs without Docker.**
