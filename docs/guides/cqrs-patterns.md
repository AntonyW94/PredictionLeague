# CQRS Patterns

This project uses CQRS (Command Query Responsibility Segregation) via MediatR. Commands and Queries have different data access patterns that **MUST** be followed.

## Core Principle

- **Commands** modify state → Use repositories
- **Queries** fetch data → Use `IApplicationReadDbConnection` with raw SQL

## Commands

Commands use repositories (`IXxxRepository`) for data access.

### Command Structure

```
PredictionLeague.Application/
└── Features/
    └── Leagues/
        └── Commands/
            └── CreateLeague/
                ├── CreateLeagueCommand.cs
                └── CreateLeagueCommandHandler.cs

PredictionLeague.Validators/
└── Leagues/
    └── CreateLeagueCommandValidator.cs

PredictionLeague.Contracts/
└── Leagues/
    └── LeagueDto.cs
```

### Command Example

```csharp
// Command definition
public record CreateLeagueCommand(
    string Name,
    int SeasonId,
    string UserId) : IRequest<LeagueDto>;

// Handler - uses repository
public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ILogger<CreateLeagueCommandHandler> _logger;

    public CreateLeagueCommandHandler(
        ILeagueRepository leagueRepository,
        ILogger<CreateLeagueCommandHandler> logger)
    {
        _leagueRepository = leagueRepository;
        _logger = logger;
    }

    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken ct)
    {
        var league = League.Create(request.SeasonId, request.Name, request.UserId);

        var created = await _leagueRepository.CreateAsync(league, ct);

        _logger.LogInformation("League (ID: {LeagueId}) created by User (ID: {UserId})",
            created.Id, request.UserId);

        return new LeagueDto(created.Id, created.Name);
    }
}
```

## Queries

**NEVER use repositories in Query handlers. ALWAYS use `IApplicationReadDbConnection` with custom SQL.**

### Why?

- Queries return DTOs, not domain models
- Raw SQL allows optimised reads with projections
- Avoids loading unnecessary data through repositories
- Better performance for complex joins and aggregations

### Query Structure

```
PredictionLeague.Application/
└── Features/
    └── Leagues/
        └── Queries/
            └── GetMyLeagues/
                ├── GetMyLeaguesQuery.cs
                └── GetMyLeaguesQueryHandler.cs

PredictionLeague.Contracts/
└── Leagues/
    └── MyLeagueDto.cs
```

### Query Example

```csharp
// CORRECT - Query using IApplicationReadDbConnection
public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly IApplicationReadDbConnection _readDb;

    public GetMyLeaguesQueryHandler(IApplicationReadDbConnection readDb)
    {
        _readDb = readDb;
    }

    public async Task<IEnumerable<MyLeagueDto>> Handle(
        GetMyLeaguesQuery request,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT
                l.[Id],
                l.[Name],
                lm.[IsApproved],
                lm.[JoinedAtUtc]
            FROM [Leagues] l
            INNER JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE lm.[UserId] = @UserId
            ORDER BY l.[Name]";

        return await _readDb.QueryAsync<MyLeagueDto>(sql, new { request.UserId }, ct);
    }
}

// WRONG - Query using repository
public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly ILeagueRepository _leagueRepository; // DON'T DO THIS

    public async Task<IEnumerable<MyLeagueDto>> Handle(...)
    {
        // This is wrong - queries should not use repositories
        var leagues = await _leagueRepository.GetByUserIdAsync(userId);
        return leagues.Select(l => new MyLeagueDto(...));
    }
}
```

## Transactional Commands

For commands that need transaction support, implement `ITransactionalRequest`:

```csharp
public record TransferLeagueOwnershipCommand(
    int LeagueId,
    string NewOwnerId) : IRequest, ITransactionalRequest;
```

The `TransactionBehaviour` pipeline will automatically wrap the handler in a `TransactionScope`.

## MediatR Pipeline Behaviours

Requests flow through these behaviours in order:

1. **ValidationBehaviour** - Runs FluentValidation validators, throws if invalid
2. **TransactionBehaviour** - Wraps `ITransactionalRequest` in TransactionScope

## Feature Organisation

Features are organised by domain area:

```
Features/
├── Authentication/
│   └── Commands/
│       ├── Login/
│       ├── Register/
│       └── Logout/
├── Dashboard/
│   └── Queries/
│       ├── GetMyLeagues/
│       └── GetLeaderboards/
├── Leagues/
│   ├── Commands/
│   │   ├── CreateLeague/
│   │   └── JoinLeague/
│   └── Queries/
│       ├── GetLeagueById/
│       └── GetLeagueDashboard/
├── Predictions/
│   └── Commands/
│       └── SubmitPredictions/
└── Admin/
    ├── Rounds/
    │   └── Commands/
    │       ├── CreateRound/
    │       └── UpdateMatchResults/
    └── Seasons/
        └── Commands/
            └── CreateSeason/
```
