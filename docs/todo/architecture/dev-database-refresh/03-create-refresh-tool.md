# Task: Create Database Refresh Tool

**Parent Feature:** [Development Database Refresh](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Build a C# console application that reads production data, anonymises personal data, and writes it to the dev database with known test accounts.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj` | Create | Project file with dependencies |
| `tools/ThePredictions.DatabaseTools/ToolMode.cs` | Create | Enum defining available modes |
| `tools/ThePredictions.DatabaseTools/Program.cs` | Create | Entry point - orchestrates the refresh |
| `tools/ThePredictions.DatabaseTools/DatabaseRefresher.cs` | Create | Core refresh/backup logic |
| `tools/ThePredictions.DatabaseTools/DataAnonymiser.cs` | Create | Personal data anonymisation logic |
| `tools/ThePredictions.DatabaseTools/TestAccountCreator.cs` | Create | Creates test player and admin accounts |
| `tools/ThePredictions.DatabaseTools/PersonalDataVerifier.cs` | Create | Verifies no real personal data remains in dev DB |
| `PredictionLeague.sln` | Modify | Add new project to solution |

## Implementation Steps

### Step 1: Create the Project

Create `tools/ThePredictions.DatabaseTools/ThePredictions.DatabaseTools.csproj`:

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

### Step 2: Create ToolMode.cs (Enum)

```csharp
public enum ToolMode
{
    DevelopmentRefresh,
    ProductionBackup
}
```

### Step 3: Create Program.cs (Entry Point)

Reads the mode argument and environment variables, then orchestrates the appropriate operation:

```csharp
try
{
    if (args.Length == 0)
        throw new InvalidOperationException("Mode required. Valid modes: " + string.Join(", ", Enum.GetNames<ToolMode>()));

    if (!Enum.TryParse<ToolMode>(args[0], ignoreCase: false, out var mode))
        throw new InvalidOperationException($"Unknown mode: '{args[0]}'. Valid modes: " + string.Join(", ", Enum.GetNames<ToolMode>()));

    var prodConnectionString = GetRequiredEnvironmentVariable("PROD_CONNECTION_STRING");

    switch (mode)
    {
        case ToolMode.DevelopmentRefresh:
            var devConnectionString = GetRequiredEnvironmentVariable("DEV_CONNECTION_STRING");
            var testPassword = GetRequiredEnvironmentVariable("TEST_ACCOUNT_PASSWORD");
            var refresher = new DatabaseRefresher(prodConnectionString, devConnectionString, testPassword, anonymise: true);
            await refresher.RunAsync();
            break;

        case ToolMode.ProductionBackup:
            var backupConnectionString = GetRequiredEnvironmentVariable("BACKUP_CONNECTION_STRING");
            var backupRefresher = new DatabaseRefresher(prodConnectionString, backupConnectionString, testPassword: null, anonymise: false);
            await backupRefresher.RunAsync();
            break;
    }

    Console.WriteLine("[SUCCESS] Operation completed successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
    return 1;
}

static string GetRequiredEnvironmentVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);

    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"{name} environment variable is not set or is empty.");

    return value;
}
```

### Step 4: Create DatabaseRefresher.cs (Core Logic)

The main orchestration class that:

1. Connects to production DB (read-only operations)
2. Reads all tables in dependency order
3. Passes data through `DataAnonymiser` to strip personal data
4. Disables foreign key constraints on dev DB
5. Truncates all dev DB tables
6. Inserts anonymised data with `SET IDENTITY_INSERT ON`
7. Re-enables foreign key constraints
8. Calls `TestAccountCreator` to add test accounts
9. Calls `PersonalDataVerifier` to verify no real personal data remains

**Table copy order** (respects foreign keys when re-enabling constraints):

```
AspNetRoles → AspNetUsers → AspNetUserRoles → AspNetUserClaims →
AspNetRoleClaims → Teams → Seasons → Rounds → Matches →
BoostDefinitions → Leagues → LeagueMembers → LeagueMemberStats →
LeagueBoostRules → LeagueBoostWindows → LeaguePrizeSettings →
UserPredictions → RoundResults → LeagueRoundResults →
UserBoostUsages → Winnings
```

**Tables to skip entirely** (delete all data, don't copy):
- `AspNetUserTokens` — user tokens, not needed in dev
- `RefreshTokens` — JWT refresh tokens, must not carry over
- `PasswordResetTokens` — password reset tokens, must not carry over

**Tables with partial copy:**
- `AspNetUserLogins` — only copy rows belonging to `antony.willson@hotmail.com` (preserves Google login for testing). Delete all other rows.

### Step 5: Create DataAnonymiser.cs

Anonymisation rules:

```csharp
const string PreservedEmail = "antony.willson@hotmail.com";

// AspNetUsers anonymisation (per row, using incrementing counter N)
// SKIP anonymisation if user.Email == PreservedEmail
user.Email = $"user{N}@testmail.com";
user.NormalizedEmail = $"USER{N}@TESTMAIL.COM";
user.UserName = $"user{N}@testmail.com";
user.NormalizedUserName = $"USER{N}@TESTMAIL.COM";
user.FirstName = $"TestUser{N}";
user.LastName = "Player";
user.PasswordHash = "INVALIDATED";
user.SecurityStamp = Guid.NewGuid().ToString();
user.PhoneNumber = null;
user.PhoneNumberConfirmed = false;
user.TwoFactorEnabled = false;
user.LockoutEnd = null;
user.AccessFailedCount = 0;

// Leagues anonymisation (per row, using incrementing counter N)
league.Name = $"League {N}";
league.EntryCode = GenerateRandomEntryCode(); // Random 6-char alphanumeric
```

### Step 6: Create TestAccountCreator.cs

Creates two known test accounts after anonymised data is inserted:

```csharp
// Use ASP.NET Identity's PasswordHasher to create proper password hashes
var hasher = new PasswordHasher<object>();
var passwordHash = hasher.HashPassword(null, testPassword);

// Insert testplayer@dev.local (User role)
// Insert testadmin@dev.local (Admin role - add to AspNetUserRoles)
// Add both to the first league as Approved members
```

**Test account details:**
- Both get new GUIDs as their `Id`
- `EmailConfirmed = true`
- `LockoutEnabled = false`
- Added to first league (lowest `Id`) with `Status = 'Approved'`
- Admin account gets the "Admin" role in `AspNetUserRoles`

### Step 7: Create PersonalDataVerifier.cs

Post-refresh verification that checks:

```csharp
// 1. No real email domains remain (check for non-testmail.com, non-dev.local emails)
//    EXCEPT antony.willson@hotmail.com which is the preserved account
// 2. No original first/last names remain (spot check against known patterns)
//    EXCEPT the preserved account
// 3. All password hashes are either "INVALIDATED" or belong to test accounts
//    or the preserved account
// 4. RefreshTokens table is empty
// 5. PasswordResetTokens table is empty
// 6. AspNetUserLogins contains ONLY rows for the preserved account
// 7. AspNetUserTokens table is empty
```

If any check fails, throw an exception to fail the GitHub Actions workflow.

## Code Patterns to Follow

### Dapper Data Access

```csharp
using var connection = new SqlConnection(connectionString);
var users = await connection.QueryAsync<dynamic>("SELECT * FROM [AspNetUsers]");
```

### Logging

Use `Console.WriteLine` with clear prefixes for the console app:

```csharp
Console.WriteLine($"[INFO] Reading {tableName} from production...");
Console.WriteLine($"[INFO] Anonymised {count} users");
Console.WriteLine($"[ERROR] Personal data verification failed: {message}");
```

### SQL Conventions

Follow the project SQL conventions — brackets around column and table names, PascalCase, table aliases (without brackets), one column per line, and each keyword on its own line with the next line indented:

```sql
SELECT
    u.[Id],
    u.[Email],
    u.[FirstName]
FROM
    [AspNetUsers] u
WHERE
    u.[Id] = @Id

SELECT
    u.[Id],
    u.[Email],
    ur.[RoleId]
FROM
    [AspNetUsers] u
JOIN [AspNetUserRoles] ur
    ON u.[Id] = ur.[UserId]
WHERE
    u.[Email] = @Email
    AND u.[EmailConfirmed] = 1

INSERT INTO [AspNetUsers] (
    [Id],
    [Email],
    [FirstName]
)
VALUES (
    @Id,
    @Email,
    @FirstName
)

UPDATE u
SET
    u.[Email] = @Email,
    u.[FirstName] = @FirstName
FROM
    [AspNetUsers] u
WHERE
    u.[Id] = @Id
```

## Verification

- [ ] Project builds without errors (`dotnet build`)
- [ ] Tool reads all tables from production
- [ ] All personal data fields are anonymised correctly
- [ ] Dev database receives anonymised data
- [ ] Test accounts are created with correct passwords
- [ ] Test accounts can log in (password hash is valid)
- [ ] Personal data verifier passes with no warnings
- [ ] Foreign key constraints are intact after refresh
- [ ] Identity columns maintain correct values

## Edge Cases to Consider

- Production database has no data in some tables (handle empty tables gracefully)
- Concurrent access to dev database during refresh (truncate + insert should be wrapped in a transaction or constraints disabled)
- Very large tables may need batched inserts
- Test account GUIDs must not clash with anonymised user GUIDs
- First league might not exist yet (handle case where Leagues table is empty)
- `NormalizedEmail` and `NormalizedUserName` must be kept in sync with `Email` and `UserName`
- League `Name` has a unique constraint on `(SeasonId, Name)` — ensure anonymised names don't clash

## Notes

- The tool uses `IDENTITY_INSERT` to preserve original IDs, maintaining all foreign key relationships
- Disabling/re-enabling FK constraints avoids issues with insert order
- The Bogus library is available for generating fake data but simple counter-based anonymisation may be sufficient for most fields
- The `Microsoft.AspNetCore.Identity` package is needed for `PasswordHasher<T>` to create valid password hashes for test accounts
