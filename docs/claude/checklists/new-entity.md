# Checklist: Creating a New Domain Entity

Use this checklist when adding a new domain entity to the project.

## Step 1: Create the Entity Class

**Location:** `PredictionLeague.Domain/Entities/{EntityName}.cs`

```csharp
public class Match
{
    // Private parameterless constructor for factory method
    private Match() { }

    // Properties with init setters for immutability
    public int Id { get; init; }
    public int RoundId { get; init; }
    public int HomeTeamId { get; init; }
    public int AwayTeamId { get; init; }
    public DateTime KickoffUtc { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    // Public constructor for Dapper hydration
    public Match(
        int id,
        int roundId,
        int homeTeamId,
        int awayTeamId,
        DateTime kickoffUtc,
        int? homeScore,
        int? awayScore,
        DateTime createdAtUtc)
    {
        Id = id;
        RoundId = roundId;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        KickoffUtc = kickoffUtc;
        HomeScore = homeScore;
        AwayScore = awayScore;
        CreatedAtUtc = createdAtUtc;
    }

    // Factory method with Guard clauses
    public static Match Create(
        int roundId,
        int homeTeamId,
        int awayTeamId,
        DateTime kickoffUtc)
    {
        Guard.Against.NegativeOrZero(roundId, nameof(roundId));
        Guard.Against.NegativeOrZero(homeTeamId, nameof(homeTeamId));
        Guard.Against.NegativeOrZero(awayTeamId, nameof(awayTeamId));
        Guard.Against.Default(kickoffUtc, nameof(kickoffUtc));

        return new Match
        {
            RoundId = roundId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            KickoffUtc = kickoffUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    // Rich domain methods (business logic)
    public bool HasStarted() => DateTime.UtcNow >= KickoffUtc;

    public bool IsCompleted() => HomeScore.HasValue && AwayScore.HasValue;
}
```

### Entity Checklist

- [ ] Private parameterless constructor
- [ ] Properties use `init` setters (immutable)
- [ ] Public constructor for Dapper hydration (all parameters)
- [ ] Static `Create` factory method with Guard clauses
- [ ] DateTime properties end with `Utc` suffix
- [ ] `CreatedAtUtc` property included
- [ ] Business logic methods where appropriate
- [ ] UK English spelling in all names

## Step 2: Create the Repository Interface

**Location:** `PredictionLeague.Domain/Interfaces/I{EntityName}Repository.cs`

```csharp
public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<Match>> GetByRoundIdAsync(int roundId, CancellationToken ct);
    Task<Match> CreateAsync(Match match, CancellationToken ct);
    Task UpdateScoreAsync(int matchId, int homeScore, int awayScore, CancellationToken ct);
    Task DeleteAsync(int matchId, CancellationToken ct);
}
```

### Interface Checklist

- [ ] One file, one interface
- [ ] All methods async with CancellationToken
- [ ] `GetByIdAsync` returns nullable
- [ ] Specific update methods (not generic `UpdateAsync`)
- [ ] `CreateAsync` returns the created entity

## Step 3: Create the Repository Implementation

**Location:** `PredictionLeague.Infrastructure/Repositories/{EntityName}Repository.cs`

```csharp
public class MatchRepository : IMatchRepository
{
    private readonly IApplicationWriteDbConnectionFactory _connectionFactory;

    public MatchRepository(IApplicationWriteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Match?> GetByIdAsync(int id, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = @"
            SELECT [Id], [RoundId], [HomeTeamId], [AwayTeamId],
                   [KickoffUtc], [HomeScore], [AwayScore], [CreatedAtUtc]
            FROM [Matches]
            WHERE [Id] = @Id";

        return await connection.QuerySingleOrDefaultAsync<Match>(sql, new { Id = id });
    }

    public async Task<Match> CreateAsync(Match match, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = @"
            INSERT INTO [Matches] ([RoundId], [HomeTeamId], [AwayTeamId], [KickoffUtc], [CreatedAtUtc])
            OUTPUT INSERTED.[Id]
            VALUES (@RoundId, @HomeTeamId, @AwayTeamId, @KickoffUtc, @CreatedAtUtc)";

        var newId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            match.RoundId,
            match.HomeTeamId,
            match.AwayTeamId,
            match.KickoffUtc,
            match.CreatedAtUtc
        });

        return new Match(
            id: newId,
            roundId: match.RoundId,
            homeTeamId: match.HomeTeamId,
            awayTeamId: match.AwayTeamId,
            kickoffUtc: match.KickoffUtc,
            homeScore: null,
            awayScore: null,
            createdAtUtc: match.CreatedAtUtc);
    }

    public async Task UpdateScoreAsync(int matchId, int homeScore, int awayScore, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = @"
            UPDATE [Matches]
            SET [HomeScore] = @HomeScore, [AwayScore] = @AwayScore
            WHERE [Id] = @Id";

        await connection.ExecuteAsync(sql, new
        {
            Id = matchId,
            HomeScore = homeScore,
            AwayScore = awayScore
        });
    }
}
```

### Repository Checklist

- [ ] Uses `IApplicationWriteDbConnectionFactory`
- [ ] SQL uses brackets around identifiers (`[TableName]`, `[ColumnName]`)
- [ ] SQL uses PascalCase column names
- [ ] SQL parameters use `@ParameterName` format
- [ ] `CreateAsync` uses `OUTPUT INSERTED.[Id]`
- [ ] `CreateAsync` returns new instance with generated ID
- [ ] Update methods are specific (not generic)
- [ ] All methods use CancellationToken

## Step 4: Register the Repository

**Location:** `PredictionLeague.Infrastructure/DependencyInjection.cs`

```csharp
services.AddScoped<IMatchRepository, MatchRepository>();
```

## Step 5: Create Database Migration (if needed)

**Location:** `PredictionLeague.Infrastructure/Migrations/`

```sql
CREATE TABLE [Matches] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [RoundId] INT NOT NULL,
    [HomeTeamId] INT NOT NULL,
    [AwayTeamId] INT NOT NULL,
    [KickoffUtc] DATETIME2 NOT NULL,
    [HomeScore] INT NULL,
    [AwayScore] INT NULL,
    [CreatedAtUtc] DATETIME2 NOT NULL,
    CONSTRAINT [FK_Matches_Rounds] FOREIGN KEY ([RoundId]) REFERENCES [Rounds]([Id]),
    CONSTRAINT [FK_Matches_HomeTeam] FOREIGN KEY ([HomeTeamId]) REFERENCES [Teams]([Id]),
    CONSTRAINT [FK_Matches_AwayTeam] FOREIGN KEY ([AwayTeamId]) REFERENCES [Teams]([Id])
);
```

### Migration Checklist

- [ ] Table name is plural (`Matches` not `Match`)
- [ ] Uses brackets around all identifiers
- [ ] `Id` is `INT IDENTITY(1,1) PRIMARY KEY`
- [ ] DateTime columns use `DATETIME2`
- [ ] Foreign keys have descriptive constraint names
- [ ] Includes `CreatedAtUtc` column

## Verification Checklist

- [ ] Entity class in `PredictionLeague.Domain/Entities/`
- [ ] Repository interface in `PredictionLeague.Domain/Interfaces/`
- [ ] Repository implementation in `PredictionLeague.Infrastructure/Repositories/`
- [ ] Repository registered in DI container
- [ ] Database migration created (if needed)
- [ ] All DateTime properties end with `Utc`
- [ ] All DateTime values use `DateTime.UtcNow`
- [ ] UK English spelling throughout
- [ ] One public type per file
