# PredictionLeague Test Suite Plan

This document outlines the comprehensive testing strategy for the PredictionLeague application.

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
10. [Test Project Structure](#10-test-project-structure)
11. [Implementation Priority](#11-implementation-priority)
12. [Package References](#12-package-references)

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
| Blazor Component Tests | ⚠️ Moderate | bUnit library |
| End-to-End Tests | ⚠️ Complex | Playwright (optional) |

---

## 2. Testing Stack

| Purpose | Tool | Version |
|---------|------|---------|
| Test Framework | xUnit.v3 | 3.x |
| Assertions | FluentAssertions | 7.x |
| Mocking | NSubstitute | 5.x |
| Integration DB | SQLite In-Memory | via Microsoft.Data.Sqlite |
| API Testing | WebApplicationFactory | Built into ASP.NET Core |
| E2E Testing | Playwright | Optional |

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

Your codebase uses MERGE in 4 repositories:
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
        // Create in-memory database
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        // Create schema
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

### 6.5 Using the Fixture in Tests

```csharp
public class GetLeagueDashboardQueryHandlerTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;
    private readonly GetLeagueDashboardQueryHandler _handler;

    public GetLeagueDashboardQueryHandlerTests(SqliteTestFixture fixture)
    {
        _fixture = fixture;
        var dbConnection = new SqliteApplicationReadDbConnection(_fixture.Connection);
        _handler = new GetLeagueDashboardQueryHandler(dbConnection);
    }

    [Fact]
    public async Task Handle_ShouldReturnDashboard_WhenUserIsMember()
    {
        // Arrange - insert test data
        await InsertTestLeague();
        await InsertTestMember();

        var query = new GetLeagueDashboardQuery
        {
            LeagueId = 1,
            UserId = "user-123"
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.LeagueName.Should().Be("Test League");
    }
}
```

### 6.6 Recommendation for MERGE Statements

Given the complexity of abstracting SQL dialects, consider this pragmatic approach:

1. **Unit test domain logic** - No database needed
2. **Unit test command handlers** - Mock repositories
3. **Integration test queries** - Use SQLite for read-only queries (most of your query handlers)
4. **Integration test MERGE operations** - Use LocalDB on Windows or Testcontainers if you need full SQL Server compatibility

Most of your **query handlers** use standard SELECT statements that work perfectly with SQLite. The MERGE statements are only in 4 repository methods used by **command handlers**, which can be unit tested with mocked repositories.

---

## 7. WebApplicationFactory for API Testing

### 7.1 How It Works

`WebApplicationFactory<TEntryPoint>` is built into ASP.NET Core and creates an **in-process test server**:

```
┌─────────────────────────────────────────────────────────┐
│                    Test Process                         │
│  ┌─────────────┐        ┌─────────────────────────────┐ │
│  │   Test      │  HTTP  │  WebApplicationFactory      │ │
│  │   Code      │───────▶│  ┌─────────────────────┐    │ │
│  │             │        │  │   Your API          │    │ │
│  │             │◀───────│  │   (Full Pipeline)   │    │ │
│  └─────────────┘        │  │   - Middleware      │    │ │
│                         │  │   - Controllers     │    │ │
│                         │  │   - MediatR         │    │ │
│                         │  │   - Validation      │    │ │
│                         │  │   - Auth            │    │ │
│                         │  └─────────────────────┘    │ │
│                         └─────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
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

            // Create SQLite in-memory connection (shared for all requests)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Initialise schema
            InitialiseDatabase(_connection);

            // Register test connection factory
            services.AddSingleton<IDbConnectionFactory>(
                new SqliteConnectionFactory(_connection));

            // Optionally override other services
            // services.AddSingleton<IEmailService, FakeEmailService>();
            // services.AddSingleton<IFootballDataService, FakeFootballDataService>();
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
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/leagues/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLeague_ShouldReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new CreateLeagueRequest
        {
            Name = "Test League",
            SeasonId = 1,
            Price = 10.00m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/leagues", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateLeague_ShouldReturnBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        await AuthenticateAsync();
        var request = new CreateLeagueRequest
        {
            Name = "",
            SeasonId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/leagues", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task AuthenticateAsync()
    {
        // Option 1: Call login endpoint and use returned token
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Password = "TestPassword123!"
        });
        var token = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // Option 2: Generate a test token directly (faster)
        // var token = GenerateTestJwtToken("user-123", "Admin");
        // _client.DefaultRequestHeaders.Authorization =
        //     new AuthenticationHeaderValue("Bearer", token);
    }
}
```

### 7.4 Testing Authentication and Authorisation

```csharp
[Fact]
public async Task GetLeague_ShouldReturnUnauthorised_WhenNoToken()
{
    // Act (no auth header set)
    var response = await _client.GetAsync("/api/leagues/1");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task AdminEndpoint_ShouldReturnForbidden_WhenUserIsNotAdmin()
{
    // Arrange - authenticate as regular user
    await AuthenticateAsRegularUserAsync();

    // Act
    var response = await _client.PostAsJsonAsync("/api/admin/rounds", new { });

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### 7.5 Seeding Test Data

```csharp
public class LeaguesApiTests : IClassFixture<PredictionLeagueApiFactory>
{
    private readonly HttpClient _client;
    private readonly PredictionLeagueApiFactory _factory;

    public LeaguesApiTests(PredictionLeagueApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Seed data before tests
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Access services to seed data
        using var scope = _factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>()
            .CreateConnection();

        // Insert test data
        connection.Execute(@"
            INSERT INTO [Seasons] ([Id], [Name], ...) VALUES (1, 'Test Season', ...);
            INSERT INTO [Users] ([Id], [Email], ...) VALUES ('user-123', 'test@example.com', ...);
        ");
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

### 8.2 How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                        Test Machine                              │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────────────┐│
│  │  Playwright │────▶│   Browser   │────▶│   Your Blazor App   ││
│  │  Test Code  │     │  (Chrome)   │     │   (localhost:5000)  ││
│  │             │◀────│             │◀────│                     ││
│  └─────────────┘     └─────────────┘     └─────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### 8.3 Visual Studio Integration

Playwright tests run in Visual Studio Test Explorer just like unit tests:

1. Install the `Microsoft.Playwright` and `Microsoft.Playwright.NUnit` (or xUnit adapter) packages
2. Run `playwright install` to download browsers
3. Tests appear in Test Explorer
4. Run/debug tests normally

**Important:** Playwright needs a running instance of your application. You can:
- Start the app manually before running tests
- Use a fixture that starts/stops the app automatically
- Use WebApplicationFactory to host the API and serve static Blazor files

### 8.4 Azure DevOps Integration

Playwright works well with Azure DevOps:

```yaml
# azure-pipelines.yml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '8.0.x'

  - script: dotnet build
    displayName: 'Build'

  - script: dotnet tool install --global Microsoft.Playwright.CLI
    displayName: 'Install Playwright CLI'

  - script: playwright install --with-deps chromium
    displayName: 'Install Browsers'

  - script: dotnet test tests/PredictionLeague.E2E.Tests
    displayName: 'Run E2E Tests'
    env:
      PLAYWRIGHT_BROWSERS_PATH: $(Build.SourcesDirectory)/browsers
```

### 8.5 Example Playwright Test

```csharp
using Microsoft.Playwright;

public class LoginTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true  // Set to false to see the browser
        });
        _page = await _browser.NewPageAsync();
    }

    [Fact]
    public async Task Login_ShouldRedirectToDashboard_WhenCredentialsAreValid()
    {
        // Navigate to login page
        await _page.GotoAsync("https://localhost:5001/login");

        // Fill in credentials
        await _page.FillAsync("[data-testid='email-input']", "test@example.com");
        await _page.FillAsync("[data-testid='password-input']", "TestPassword123!");

        // Click login button
        await _page.ClickAsync("[data-testid='login-button']");

        // Wait for navigation
        await _page.WaitForURLAsync("**/dashboard");

        // Assert we're on the dashboard
        var heading = await _page.TextContentAsync("h1");
        heading.Should().Contain("Dashboard");
    }

    [Fact]
    public async Task Login_ShouldShowError_WhenCredentialsAreInvalid()
    {
        await _page.GotoAsync("https://localhost:5001/login");

        await _page.FillAsync("[data-testid='email-input']", "wrong@example.com");
        await _page.FillAsync("[data-testid='password-input']", "wrongpassword");
        await _page.ClickAsync("[data-testid='login-button']");

        // Wait for error message
        var errorMessage = await _page.TextContentAsync(".error-message");
        errorMessage.Should().Contain("Invalid credentials");
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}
```

### 8.6 Playwright Recommendation

**Consider Playwright optional** for this project because:
- API integration tests catch most bugs
- Blazor component tests (with bUnit) can test UI logic
- E2E tests are slower and more brittle
- Higher maintenance cost

**Use Playwright for:**
- Critical user journeys (login, submit predictions, view leaderboard)
- Smoke tests before deployment
- Visual regression testing (if needed)

---

## 9. Catching SQL and Dapper Mapping Errors

This section addresses your main pain points:
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
// Error: No matching constructor found (needs ExactScoreCount parameter)

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

### 9.3 Repository Integration Tests with SQLite

These tests execute real SQL against a real (SQLite) database:

```csharp
public class LeagueRepositoryTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;
    private readonly LeagueRepository _repository;

    public LeagueRepositoryTests(SqliteTestFixture fixture)
    {
        _fixture = fixture;
        _repository = new LeagueRepository(new SqliteConnectionFactory(fixture.Connection));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldMapAllColumns_WhenLeagueExists()
    {
        // Arrange - insert a league with ALL columns populated
        await InsertLeague(new
        {
            Id = 1,
            Name = "Test League",
            SeasonId = 1,
            Price = 10.00m,
            AdministratorUserId = "user-123",
            EntryCode = "ABC123",
            CreatedAtUtc = DateTime.UtcNow,
            EntryDeadlineUtc = DateTime.UtcNow.AddDays(30),
            PointsForExactScore = 3,
            PointsForCorrectResult = 1,
            IsFree = false,
            HasPrizes = true,
            PrizeFundOverride = (decimal?)null
        });

        // Act - this will FAIL if any column is missing from the SELECT
        var result = await _repository.GetByIdAsync(1, CancellationToken.None);

        // Assert - verify ALL properties are mapped correctly
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test League");
        result.SeasonId.Should().Be(1);
        result.Price.Should().Be(10.00m);
        result.AdministratorUserId.Should().Be("user-123");
        result.EntryCode.Should().Be("ABC123");
        result.PointsForExactScore.Should().Be(3);
        result.PointsForCorrectResult.Should().Be(1);
        result.IsFree.Should().BeFalse();
        result.HasPrizes.Should().BeTrue();
        result.PrizeFundOverride.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertCorrectValues_WhenLeagueIsValid()
    {
        // Arrange
        var league = League.Create(
            seasonId: 1,
            name: "New League",
            // ... all parameters
        );

        // Act
        var result = await _repository.CreateAsync(league, CancellationToken.None);

        // Assert - verify the data was inserted correctly
        var inserted = await GetLeagueDirectly(result.Id);
        inserted.Name.Should().Be("New League");
        inserted.SeasonId.Should().Be(1);
        // Verify ALL columns to catch parameter order bugs
    }

    private async Task<dynamic> GetLeagueDirectly(int id)
    {
        const string sql = "SELECT * FROM [Leagues] WHERE [Id] = @Id";
        return await _fixture.Connection.QuerySingleAsync(sql, new { Id = id });
    }
}
```

### 9.4 Query Handler Integration Tests

These catch missing columns in SELECT statements:

```csharp
public class GetLeagueDashboardQueryHandlerTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture;
    private readonly GetLeagueDashboardQueryHandler _handler;

    public GetLeagueDashboardQueryHandlerTests(SqliteTestFixture fixture)
    {
        _fixture = fixture;
        var dbConnection = new SqliteApplicationReadDbConnection(fixture.Connection);
        _handler = new GetLeagueDashboardQueryHandler(dbConnection);
    }

    [Fact]
    public async Task Handle_ShouldMapAllRoundProperties_WhenRoundsExist()
    {
        // Arrange
        await SeedLeagueAndRounds();

        var query = new GetLeagueDashboardQuery
        {
            LeagueId = 1,
            UserId = "user-123",
            IsAdmin = false
        };

        // Act - this will FAIL if RoundDto properties don't match SELECT columns
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - verify mapping
        result.Should().NotBeNull();
        result!.ViewableRounds.Should().NotBeEmpty();

        var round = result.ViewableRounds.First();
        round.Id.Should().BeGreaterThan(0);
        round.RoundNumber.Should().BeGreaterThan(0);
        round.Status.Should().NotBeNullOrEmpty();
        round.MatchCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
```

### 9.5 Compile-Time Safety with Source Generators (Advanced)

For additional safety, consider using Dapper's AOT source generator (preview) which provides compile-time checking:

```csharp
// With Dapper.AOT, this would give a compile-time error if parameters don't match
[Command(@"INSERT INTO [Leagues] ([Name], [SeasonId]) VALUES (@Name, @SeasonId)")]
public partial Task<int> CreateLeagueAsync(string name, int seasonId);
```

This is a newer feature and may not be production-ready yet.

### 9.6 Recommended Testing Strategy for SQL/Dapper Errors

```
┌─────────────────────────────────────────────────────────────────┐
│                     Testing Strategy                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Priority 1: Query Handler Integration Tests                     │
│  ─────────────────────────────────────────────                   │
│  • Test all 67 query handlers with SQLite                        │
│  • Catches: missing columns, wrong column names, type mismatches │
│  • These are your most common bugs                               │
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

## 10. Test Project Structure

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
└── tests/
    ├── PredictionLeague.Domain.Tests/
    │   ├── Models/
    │   │   ├── LeagueTests.cs
    │   │   ├── RoundTests.cs
    │   │   └── UserPredictionTests.cs
    │   └── Services/
    │       ├── BoostEligibilityEvaluatorTests.cs
    │       └── PredictionDomainServiceTests.cs
    │
    ├── PredictionLeague.Application.Tests/
    │   └── Features/
    │       ├── Leagues/
    │       │   └── Commands/
    │       │       ├── CreateLeagueCommandHandlerTests.cs
    │       │       └── JoinLeagueCommandHandlerTests.cs
    │       └── Predictions/
    │           └── Commands/
    │               └── SubmitPredictionsCommandHandlerTests.cs
    │
    ├── PredictionLeague.Validators.Tests/
    │   ├── CreateLeagueRequestValidatorTests.cs
    │   ├── SubmitPredictionsRequestValidatorTests.cs
    │   └── ...
    │
    ├── PredictionLeague.Infrastructure.Tests/
    │   ├── Repositories/
    │   │   ├── LeagueRepositoryTests.cs
    │   │   └── UserPredictionRepositoryTests.cs
    │   ├── Queries/
    │   │   ├── GetLeagueDashboardQueryHandlerTests.cs
    │   │   └── GetLeaderboardQueryHandlerTests.cs
    │   └── Services/
    │       └── BoostServiceTests.cs
    │
    ├── PredictionLeague.API.Tests/
    │   ├── Controllers/
    │   │   ├── LeaguesControllerTests.cs
    │   │   ├── PredictionsControllerTests.cs
    │   │   └── AuthControllerTests.cs
    │   └── PredictionLeagueApiFactory.cs
    │
    ├── PredictionLeague.E2E.Tests/ (Optional)
    │   ├── LoginTests.cs
    │   └── PredictionFlowTests.cs
    │
    └── PredictionLeague.Tests.Shared/
        ├── Fixtures/
        │   ├── SqliteTestFixture.cs
        │   └── SqliteConnectionFactory.cs
        ├── Builders/
        │   ├── LeagueBuilder.cs
        │   └── RoundBuilder.cs
        └── Extensions/
            └── TestDataExtensions.cs
```

---

## 11. Implementation Priority

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

This is where you'll catch your main pain points (missing columns, wrong mappings).

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

### Phase 7: E2E Tests (Optional)
**Effort: High | Value: Medium**

Only if you want browser-based testing.

---

## 12. Package References

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

### PredictionLeague.E2E.Tests.csproj (Optional)

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

---

## Summary

| Question | Answer |
|----------|--------|
| **SQLite without Docker?** | Yes - runs entirely in Visual Studio, no containers |
| **MERGE statement compatibility?** | Not supported - use SQL dialect abstraction or test at API level |
| **WebApplicationFactory?** | In-process test server, full HTTP pipeline, no external processes |
| **Playwright in VS/Azure DevOps?** | Yes to both - runs in Test Explorer, works in pipelines |
| **Tests for SQL mapping bugs?** | Query handler and repository integration tests with SQLite |

**Key Takeaway:** Your main pain points (SQL parameter order, missing columns) require **integration tests** with a real database. Unit tests with mocked repositories won't catch these issues. Start with query handler integration tests using SQLite - this gives the highest return for catching your specific bugs.
