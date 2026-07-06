# Transaction Context Hardening - Nesting-Safe Transactions, Explicit Rollback, Post-Commit Side Effects

## Status

**Not Started** | In Progress | Complete

## Priority

High. Three failure modes exist today, all invisible to the compiler and to unit tests:

1. `DbTransactionContext.BeginAsync` throws `InvalidOperationException("A transaction is already active.")` on any nested begin. Handler-to-handler `mediator.Send` chaining is the blessed house pattern (June 2026 audit, decision 10), so this is a landmine: marking any currently nested command (for example `ProcessPrizesCommand`) as `ITransactionalRequest` would break `UpdateMatchResultsCommandHandler` at runtime with no build or test failure.
2. `TransactionBehaviour`'s catch block logs "Rolling back" but never rolls back, and never resets the context's begun state. After a failed transactional request, any subsequent `mediator.Send` of another transactional request in the same DI scope throws "A transaction is already active."
3. External side effects run inside open SQL transactions: `CreateSeasonCommandHandler` holds its transaction open across slow Football API HTTP calls (via the nested `SyncSeasonWithApiCommand`), and `JoinLeagueCommandHandler` / `UpdateLeagueCommandHandler` / `UpdateLeagueMemberStatusCommandHandler` send Brevo emails mid-transaction. The notify handlers even work around their own caller's row locks by reading on a second connection through `IApplicationReadDbConnection`, which violates CQRS rule 1 (commands must not use the read connection) - June 2026 audit item 2.2 already decided to restructure this.

This plan implements audit items 2.2 (restructure the notify commands to carry their data), 2.9 (explicit rollback in `TransactionBehaviour`) and the documentation action from 2.7 (why `SyncSeasonWithApiCommand` must stay non-transactional), all from `docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md`.

## Repo conventions the executor MUST follow

Restated from `CLAUDE.md` so nothing in this plan is executed in violation of them:

- **UK English spelling** everywhere (`behaviour`, `organise`, `serialise`). Never US English.
- **No em dashes or en dashes** in any authored text (code, comments, commits, this doc). Plain `-` only.
- **One public type per file.** The two co-located public DTO records deleted by this plan (`LeagueAdminDto`, `LeagueMemberContactDto`) are existing violations of this rule.
- **`DateTime.UtcNow` only, and only via `IDateTimeProvider`** in handlers. No new direct `DateTime.Now`/`DateTime.UtcNow` calls.
- **CQRS rules:** command handlers use repositories (`IXxxRepository`) and never `IApplicationReadDbConnection`; query handlers use `IApplicationReadDbConnection` and never repositories.
- **Test naming:** `MethodName_ShouldX_WhenY()`. xUnit + FluentAssertions + NSubstitute.
- **xUnit1051:** always pass `CancellationToken.None` in tests, never a bare `default` - including inside `DidNotReceiveWithAnyArgs()` verifications. CI runs `/p:TreatWarningsAsErrors=true` and this warning fails the build.
- **Entities in tests:** construct with the full public hydration constructor and an explicit ID, not the `Create(...)` factory.
- **Statements on a new line after `if`** (no single-line `if (x) return;`).
- **Logging format:** `"EntityName (ID: {EntityNameId})"` where an entity ID is logged.
- **No new `.sql` files** anywhere (this plan needs none - there are no database schema changes).
- **NuGet:** one package addition is required (NSubstitute in the Composition test project). Pin it to `5.3.0` to match the version already used by `tests/Unit/ThePredictions.Application.Tests.Unit/ThePredictions.Application.Tests.Unit.csproj`, keeping the solution on a single NSubstitute version.
- Domain project code is **not** touched by this plan, so the 100% Domain coverage gate is unaffected (running `tools\Test Coverage\coverage-unit.bat` at the end is still a safe confirmation).

## Verified current state (read before editing; if the code has drifted, follow the code)

### The transaction context and how repositories join it

`src/ThePredictions.Infrastructure/Data/DbTransactionContext.cs` (registered scoped in `src/ThePredictions.Infrastructure/DependencyInjection.cs` line 41: `services.AddScoped<IDbTransactionContext, DbTransactionContext>();`):

```csharp
public class DbTransactionContext(IDbConnectionFactory connectionFactory) : IDbTransactionContext, IAsyncDisposable, IDisposable
{
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _begun;

    public bool HasActiveTransaction => _begun;
    // Connection/Transaction getters throw if !_begun; the connection and the real
    // IDbTransaction are created lazily on first Connection access.

    public Task BeginAsync(CancellationToken cancellationToken)
    {
        if (_begun)
            throw new InvalidOperationException("A transaction is already active.");

        _begun = true;
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction == null)
            return Task.CompletedTask;

        _transaction.Commit();
        return Task.CompletedTask;
    }
    // Dispose() disposes transaction + connection and resets _begun; disposing an
    // uncommitted IDbTransaction rolls it back, which is the only rollback path today.
}
```

`src/ThePredictions.Infrastructure/Repositories/RepositoryBase.cs` - every repository joins the ambient transaction through this base, so all repository work inside a begun scope shares one connection and one transaction:

```csharp
public abstract class RepositoryBase(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
{
    protected IDbConnection Connection => transactionContext.HasActiveTransaction
        ? transactionContext.Connection
        : connectionFactory.CreateConnection();

    protected IDbTransaction? Transaction => transactionContext.HasActiveTransaction
        ? transactionContext.Transaction
        : null;
}
```

`src/ThePredictions.Application/Data/IDbTransactionContext.cs` has no rollback member:

```csharp
public interface IDbTransactionContext
{
    bool HasActiveTransaction { get; }
    IDbConnection Connection { get; }
    IDbTransaction Transaction { get; }
    Task BeginAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
}
```

### The behaviour

`src/ThePredictions.Application/Common/Behaviours/TransactionBehaviour.cs` - the catch logs but neither rolls back nor resets state:

```csharp
public class TransactionBehaviour<TRequest, TResponse>(
    IDbTransactionContext transactionContext,
    ILogger<TransactionBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>, ITransactionalRequest
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        await transactionContext.BeginAsync(cancellationToken);

        try
        {
            logger.LogDebug("Beginning transaction for {RequestName}", requestName);

            var response = await next(cancellationToken);

            await transactionContext.CommitAsync(cancellationToken);

            logger.LogDebug("Committed transaction for {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transaction for {RequestName} failed. Rolling back.", requestName);
            throw;
        }
    }
}
```

Pipeline registration, `src/ThePredictions.API/DependencyInjection.cs` lines 220-229 (`ValidationBehaviour` runs outside `TransactionBehaviour`; MediatR 14.0.0):

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(IAssemblyMarker).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehaviour<,>));

    var mediatRKey = configuration["MediatR:LicenceKey"];
    if (!string.IsNullOrEmpty(mediatRKey))
        cfg.LicenseKey = mediatRKey;
});
```

### ITransactionalRequest inventory (verified by grep)

Transactional today: `SubmitPredictionsCommand`, `SetLeagueBoostRulesCommand`, `LogoutCommand`, `SetPrizeSchemeCommand`, `SetLeagueArchivedCommand`, `RemoveRejectedLeagueCommand`, `DeleteLeagueCommand`, `DefinePrizeStructureCommand`, `CreateLeagueCommand`, `JoinLeagueCommand`, `DeleteUserCommand`, `CreateRoundCommand`, `SendScheduledRemindersCommand`, `UpdateMatchResultsCommand`, `UpdateRoundCommand`, `CreateSeasonCommand`, `DeleteSeasonCommand`.

NOT transactional today (sent nested from inside transactional handlers): `SyncSeasonWithApiCommand` (nested in `CreateSeasonCommandHandler` line 47), `ProcessPrizesCommand` (nested in `UpdateMatchResultsCommandHandler` line 108 and `RecalculateSeasonStatsCommandHandler` line 40), `PublishUpcomingRoundsCommand` (nested in `SyncSeasonWithApiCommandHandler` lines 293 and 490, also sent top-level from `TasksController` line 70), `NotifyMemberOfLeagueApprovalCommand` / `NotifyLeagueAdminOfJoinRequestCommand` (nested in the three league handlers), `SendRoundDigestEmailsCommand` / `SendPrizeNotificationsCommand` (nested in `UpdateMatchResultsCommandHandler`). Also not transactional: `UpdateLeagueCommand`, `UpdateLeagueMemberStatusCommand`, `RecalculateSeasonStatsCommand`.

The chains only work because no nested command is itself `ITransactionalRequest`. Nothing enforces that; this plan makes nesting safe instead of relying on luck.

### The side-effect escapes

- `CreateSeasonCommandHandler` (`src/ThePredictions.Application/Features/Admin/Seasons/Commands/CreateSeasonCommandHandler.cs` lines 46-47) sends `SyncSeasonWithApiCommand` mid-transaction. Sync's repository writes join the ambient transaction via `RepositoryBase`, so its deliberate round-by-round short-lock design (audit decision 7) is defeated whenever it runs nested: every lock is held until `CreateSeasonCommand` commits, across many Football API HTTP calls.
- `JoinLeagueCommandHandler.NotifyAsync` (lines 47-74) sends the notify commands inside the join transaction. The notify handlers (`NotifyLeagueAdminOfJoinRequestCommandHandler`, `NotifyMemberOfLeagueApprovalCommandHandler`) then read `[AspNetUsers] CROSS JOIN [Seasons]` via `IApplicationReadDbConnection` on a second connection, with comments explaining they deliberately avoid the `[Leagues]` row locked by the in-flight join transaction. Each handler also declares a second public record at the bottom of its file (`LeagueAdminDto` at `NotifyLeagueAdminOfJoinRequestCommandHandler.cs:66`, `LeagueMemberContactDto` at `NotifyMemberOfLeagueApprovalCommandHandler.cs:69`).
- `UpdateLeagueCommandHandler` (line 54-57) and `UpdateLeagueMemberStatusCommandHandler` (line 43-44) send `NotifyMemberOfLeagueApprovalCommand` after their repository writes. These two commands are not currently transactional, so today the emails are not inside a transaction, but their multi-statement writes are also not atomic; this plan makes them transactional AND moves the emails after commit.

## Design

### Decision 1: reference-counted re-entrancy in DbTransactionContext

Nested `BeginAsync` joins the outer transaction by incrementing a depth counter; only the outermost `CommitAsync`/`RollbackAsync` touches the real `IDbTransaction`. Chosen over the alternatives because:

- **Matches the blessed pattern.** Audit decision 10 keeps handler chaining; ref counting makes any chain of transactional commands compose into one atomic unit with zero changes at call sites.
- **SQL savepoints** (partial inner rollback) add complexity Dapper does not need here; every current chain wants all-or-nothing semantics for the outer command.
- **`TransactionScope`** with async flow risks distributed-transaction promotion and fights the existing `RepositoryBase` lazy-connection design.
- **Throw-on-nested plus an analyser/convention test** leaves the composition problem unsolved; it merely documents the landmine.

Rollback semantics: a rollback at ANY depth immediately rolls back the real transaction (a SQL rollback aborts everything regardless), marks the context rolled back, and dooms the remaining outer frames: `Connection`/`Transaction`/`CommitAsync`/`BeginAsync` all throw until every frame has unwound via `RollbackAsync`. When the depth returns to zero the state fully resets, so the same DI scope can begin a fresh transaction afterwards (fixing failure mode 2). Commit at inner depth is provisional: if the outer scope later rolls back, the inner work rolls back with it - that is the intended atomicity.

### Decision 2: explicit RollbackAsync on the interface, called by TransactionBehaviour's catch

`IDbTransactionContext` gains `Task RollbackAsync(CancellationToken cancellationToken)`. The behaviour's catch calls it before rethrowing. Dispose-time rollback remains as a backstop only.

### Decision 3: post-commit queue drained by TransactionBehaviour

Handlers stop `mediator.Send`-ing side-effect commands mid-transaction. Instead they enqueue the fully-populated command on a new scoped `IPostCommitQueue`; after the OUTERMOST commit succeeds, `TransactionBehaviour` drains the queue and dispatches each command through `IMediator`. Options evaluated against the actual pipeline ordering (`ValidationBehaviour` outside `TransactionBehaviour`, behaviour per request, scoped context shared across nested sends):

- **(chosen) Scoped queue in Application, drained by TransactionBehaviour after commit.** Keeps orchestration in handlers (decision 10), needs no controller changes, works identically for the join endpoints, league update, member status and season-create sync, and is trivially unit-testable. The behaviour already owns the transaction lifecycle, so "after commit" has exactly one authoritative location. Nested transactional commands are handled naturally: after an inner commit `HasActiveTransaction` is still true, so only the outermost behaviour dispatches.
- **Controller/caller sends the notify command after the transactional command returns.** Rejected: pushes orchestration to the edge (contradicts decision 10), needs the transactional commands to return notification payloads through their response types, and duplicates the wiring across four call sites in `LeaguesController`.
- **Callback/hook list on DbTransactionContext itself.** Rejected: the context lives in Infrastructure and would need to invoke MediatR (an Application concern) or store captured delegates, which is harder to test and muddies the layering. The queue lives in Application where the senders and the dispatcher both are.

Failure handling: a post-commit command that throws is logged and swallowed - the database work is already committed and an email/HTTP failure must not turn a succeeded request into a 500. On rollback the queue is cleared (once fully unwound), so a failed join never emails anyone. Post-commit commands run inside the same HTTP request, after commit and before the controller returns, so caller-visible timing is unchanged.

`PostCommitQueue.Enqueue` throws if no transaction is active. This fail-fast guard means a command that enqueues side effects MUST be `ITransactionalRequest` - which is why steps 8-10 mark `UpdateLeagueCommand` and `UpdateLeagueMemberStatusCommand` transactional in the same change.

### Decision 4: notify commands carry their data (audit 2.2)

`NotifyLeagueAdminOfJoinRequestCommand` and `NotifyMemberOfLeagueApprovalCommand` are extended to carry recipient email, recipient first name and season name. Their handlers lose `IApplicationReadDbConnection` entirely (restoring CQRS rule 1) and the co-located public DTOs are deleted (restoring one-type-per-file). The sending handlers gather the data with write-side services they may legally use: `ISeasonRepository` for the season name and the existing `IUserManager` abstraction (`src/ThePredictions.Application/Services/IUserManager.cs`, `Task<ApplicationUser?> FindByIdAsync(string userId)` - note: no CancellationToken parameter) for recipient email/first name. `[AspNetUsers]` and `[Seasons]` rows are not locked by these transactions, so these reads are deadlock-free, and the lock-dodging comment/workaround becomes unnecessary.

### Constraint restated (audit 2.7, decision 7)

`SyncSeasonWithApiCommand` is deliberately non-transactional: round-by-round persistence keeps database locks short during slow external API calls, and the sync is re-runnable and self-healing. It must NOT become `ITransactionalRequest`, and this plan does not make it one. Instead, step 11 stops it inheriting `CreateSeasonCommand`'s ambient transaction by dispatching it post-commit, and step 13 adds the explanatory comment the audit asked for.

---

## Implementation steps

Execute in order. All paths are relative to the repository root.

### Step 1 - Add RollbackAsync to the interface

File: `src/ThePredictions.Application/Data/IDbTransactionContext.cs`

Replace the entire file content with:

```csharp
using System.Data;

namespace ThePredictions.Application.Data;

public interface IDbTransactionContext
{
    bool HasActiveTransaction { get; }
    IDbConnection Connection { get; }
    IDbTransaction Transaction { get; }
    Task BeginAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}
```

### Step 2 - Rewrite DbTransactionContext with reference-counted re-entrancy

File: `src/ThePredictions.Infrastructure/Data/DbTransactionContext.cs`

Replace the entire file content with:

```csharp
using ThePredictions.Application.Data;
using System.Data;

namespace ThePredictions.Infrastructure.Data;

/// <summary>
/// Scoped ambient transaction shared by every repository in the request (via RepositoryBase).
/// Re-entrant: a nested BeginAsync joins the outer transaction (depth counting) so chained
/// mediator.Send calls between ITransactionalRequest handlers compose into one atomic unit.
/// Only the outermost CommitAsync commits; a RollbackAsync at any depth rolls the whole
/// transaction back immediately and dooms the remaining outer frames until they unwind.
/// Once fully unwound (committed or rolled back), the state resets and the same scope can
/// begin a fresh transaction.
/// </summary>
public class DbTransactionContext(IDbConnectionFactory connectionFactory) : IDbTransactionContext, IAsyncDisposable, IDisposable
{
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private int _depth;
    private bool _rolledBack;

    public bool HasActiveTransaction => _depth > 0;

    public IDbConnection Connection
    {
        get
        {
            if (_depth == 0)
                throw new InvalidOperationException("No active transaction. Call BeginAsync first.");

            if (_rolledBack)
                throw new InvalidOperationException("The transaction has been rolled back and can no longer be used.");

            if (_connection == null)
            {
                _connection = connectionFactory.CreateConnection();
                _connection.Open();
                _transaction = _connection.BeginTransaction();
            }

            return _connection;
        }
    }

    public IDbTransaction Transaction
    {
        get
        {
            if (_depth == 0)
                throw new InvalidOperationException("No active transaction. Call BeginAsync first.");

            if (_transaction == null)
            {
                // Accessing Connection triggers lazy initialisation
                _ = Connection;
            }

            return _transaction!;
        }
    }

    public Task BeginAsync(CancellationToken cancellationToken)
    {
        if (_rolledBack)
            throw new InvalidOperationException("The transaction has been rolled back; a new scope cannot begin until the rolled-back scopes have unwound.");

        _depth++;
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_depth == 0)
            throw new InvalidOperationException("No active transaction. Call BeginAsync first.");

        if (_rolledBack)
            throw new InvalidOperationException("The transaction has been rolled back and cannot be committed.");

        _depth--;

        // A nested scope's commit is provisional; the outermost owner performs the real commit.
        if (_depth > 0)
            return Task.CompletedTask;

        try
        {
            _transaction?.Commit();
        }
        finally
        {
            ResetState();
        }

        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        // Idempotent once fully unwound, so a catch block can always call it safely.
        if (_depth == 0)
            return Task.CompletedTask;

        _depth--;

        try
        {
            // The first rollback (at any depth) aborts the real transaction; outer frames
            // unwinding through their own RollbackAsync only decrement the depth.
            if (!_rolledBack)
            {
                _rolledBack = true;
                _transaction?.Rollback();
            }
        }
        finally
        {
            if (_depth == 0)
                ResetState();
        }

        return Task.CompletedTask;
    }

    private void ResetState()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
        _transaction = null;
        _connection = null;
        _rolledBack = false;
    }

    public void Dispose()
    {
        // Backstop for abandoned scopes: disposing an uncommitted IDbTransaction rolls it back.
        ResetState();
        _depth = 0;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
```

Notes for the executor:

- `RepositoryBase` needs no changes; `HasActiveTransaction => _depth > 0` preserves its join semantics.
- The original `CommitAsync` silently no-opped when no transaction had begun; the new version throws at depth 0. `TransactionBehaviour` is the only caller of `BeginAsync`/`CommitAsync`/`RollbackAsync` (verified by grep), and it always pairs them, so this tightening is safe.
- The lazy connection design is kept: if a transactional handler never touches a repository, no SQL connection or transaction is ever opened, and `CommitAsync`'s `_transaction?.Commit()` is a harmless no-op.

### Step 3 - New interface IPostCommitQueue

New file: `src/ThePredictions.Application/Common/Interfaces/IPostCommitQueue.cs`

```csharp
namespace ThePredictions.Application.Common.Interfaces;

/// <summary>
/// Scoped queue of MediatR commands to dispatch after the current transaction commits.
/// Handlers running inside an ITransactionalRequest enqueue side-effect commands (emails,
/// external API syncs) here instead of sending them mid-transaction; TransactionBehaviour
/// drains the queue after the outermost commit and clears it on rollback.
/// </summary>
public interface IPostCommitQueue
{
    void Enqueue(object command);
    IReadOnlyList<object> Drain();
    void Clear();
}
```

### Step 4 - New implementation PostCommitQueue

New file: `src/ThePredictions.Application/Common/PostCommitQueue.cs`

```csharp
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Data;

namespace ThePredictions.Application.Common;

public class PostCommitQueue(IDbTransactionContext transactionContext) : IPostCommitQueue
{
    private readonly List<object> _commands = [];

    public void Enqueue(object command)
    {
        // Without an active transaction nothing would ever drain the queue, so the command
        // would be silently lost. Fail fast: the enqueuing request must be ITransactionalRequest.
        if (!transactionContext.HasActiveTransaction)
            throw new InvalidOperationException("Post-commit commands can only be queued while a transaction is active. Mark the request as ITransactionalRequest.");

        _commands.Add(command);
    }

    public IReadOnlyList<object> Drain()
    {
        var drained = _commands.ToList();
        _commands.Clear();
        return drained;
    }

    public void Clear() => _commands.Clear();
}
```

### Step 5 - Rewrite TransactionBehaviour (explicit rollback + post-commit dispatch)

File: `src/ThePredictions.Application/Common/Behaviours/TransactionBehaviour.cs`

Replace the entire file content with:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Data;

namespace ThePredictions.Application.Common.Behaviours;

public class TransactionBehaviour<TRequest, TResponse>(
    IDbTransactionContext transactionContext,
    IPostCommitQueue postCommitQueue,
    IMediator mediator,
    ILogger<TransactionBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>, ITransactionalRequest
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        await transactionContext.BeginAsync(cancellationToken);

        try
        {
            logger.LogDebug("Beginning transaction for {RequestName}", requestName);

            var response = await next(cancellationToken);

            await transactionContext.CommitAsync(cancellationToken);

            logger.LogDebug("Committed transaction for {RequestName}", requestName);

            // Only the outermost scope's commit is real. A nested scope still has an active
            // transaction here and must leave the queued side effects for the outermost owner.
            if (!transactionContext.HasActiveTransaction)
                await DispatchPostCommitCommandsAsync(requestName, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transaction for {RequestName} failed. Rolling back.", requestName);

            await transactionContext.RollbackAsync(cancellationToken);

            // Once fully unwound, drop the side effects queued by the failed work so a
            // rolled-back request never emails anyone or triggers an external sync.
            if (!transactionContext.HasActiveTransaction)
                postCommitQueue.Clear();

            throw;
        }
    }

    private async Task DispatchPostCommitCommandsAsync(string requestName, CancellationToken cancellationToken)
    {
        foreach (var command in postCommitQueue.Drain())
        {
            try
            {
                await mediator.Send(command, cancellationToken);
            }
            catch (Exception ex)
            {
                // The transaction has committed; a failing side effect (an email, an external
                // sync) must not fail the request or undo committed work. Log and continue.
                logger.LogError(ex, "Post-commit command {CommandName} failed after {RequestName} committed", command.GetType().Name, requestName);
            }
        }
    }
}
```

### Step 6 - Register the queue in DI

File: `src/ThePredictions.API/DependencyInjection.cs`, method `AddApplicationServices` (around line 211).

Current code:

```csharp
        private static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IPrizeEvaluator, PrizeEvaluator>();
            services.AddScoped<IPrizeEvaluationInputsReader, PrizeEvaluationInputsReader>();
            services.AddScoped<IPrizeSchemeFreezeService, PrizeSchemeFreezeService>();
```

Target code (one line added; it must be **scoped** - the queue is shared between the behaviour and the handlers within one request scope):

```csharp
        private static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IPrizeEvaluator, PrizeEvaluator>();
            services.AddScoped<IPrizeEvaluationInputsReader, PrizeEvaluationInputsReader>();
            services.AddScoped<IPrizeSchemeFreezeService, PrizeSchemeFreezeService>();
            services.AddScoped<IPostCommitQueue, PostCommitQueue>();
```

Add the required `using` directives to the top of the file if not already present: `using ThePredictions.Application.Common;` and `using ThePredictions.Application.Common.Interfaces;` (check what is already imported; `ITransactionalRequest` lives in the latter but may not be imported today).

`tests/Unit/ThePredictions.Composition.Tests.Unit/ContainerValidationTests.cs` builds the real container and resolves every handler, so a missed registration fails there in CI.

### Step 7 - Restructure the notify commands and handlers (audit 2.2)

**7a.** File: `src/ThePredictions.Application/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommand.cs` - replace the entire file content with:

```csharp
using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record NotifyLeagueAdminOfJoinRequestCommand(
    string AdminEmail,
    string AdminFirstName,
    string LeagueName,
    string SeasonName,
    string NewMemberFirstName,
    string NewMemberLastName,
    string? LeagueUrlBase = null) : IRequest;
```

**7b.** File: `src/ThePredictions.Application/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandler.cs` - replace the entire file content with (this removes `IApplicationReadDbConnection`, the SQL, the lock-dodging comment and the co-located `LeagueAdminDto` record):

```csharp
using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyLeagueAdminOfJoinRequestCommandHandler(IEmailService emailService, IOptions<BrevoSettings> brevoSettings) : IRequestHandler<NotifyLeagueAdminOfJoinRequestCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;

    public async Task Handle(NotifyLeagueAdminOfJoinRequestCommand request, CancellationToken cancellationToken)
    {
        if (_brevoSettings.Templates == null)
            return;

        var templateId = _brevoSettings.Templates.JoinLeagueRequest;

        // The command carries everything the email needs (recipient, names, season name),
        // supplied by the caller which already holds the aggregates. This handler is dispatched
        // after the caller's transaction commits and performs no database access at all.
        var parameters = new
        {
            FIRST_NAME = request.NewMemberFirstName,
            LAST_NAME = request.NewMemberLastName,
            LEAGUE_NAME = request.LeagueName,
            SEASON_NAME = request.SeasonName,
            ADMIN_NAME = request.AdminFirstName,
            DASHBOARD_URL = BuildAdminDashboardUrl(request.LeagueUrlBase)
        };

        await emailService.SendTemplatedEmailAsync(request.AdminEmail, templateId, parameters);
    }

    // Deep-links to the dashboard's Admin tab, where pending join requests are actioned. The base comes
    // from the request origin (the join is HTTP-triggered); falls back to the canonical site if absent.
    private static string BuildAdminDashboardUrl(string? leagueUrlBase)
    {
        var baseUrl = string.IsNullOrWhiteSpace(leagueUrlBase)
            ? "https://www.thepredictions.co.uk"
            : leagueUrlBase.TrimEnd('/');

        return $"{baseUrl}/dashboard?tab=admin";
    }
}
```

**7c.** File: `src/ThePredictions.Application/Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommand.cs` - replace the entire file content with:

```csharp
using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record NotifyMemberOfLeagueApprovalCommand(
    string MemberEmail,
    string MemberFirstName,
    int LeagueId,
    string LeagueName,
    string SeasonName,
    string? LeagueUrlBase) : IRequest;
```

**7d.** File: `src/ThePredictions.Application/Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommandHandler.cs` - replace the entire file content with (removes `IApplicationReadDbConnection`, the SQL and the co-located `LeagueMemberContactDto` record; keeps the template-id-0 skip):

```csharp
using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class NotifyMemberOfLeagueApprovalCommandHandler(IEmailService emailService, IOptions<BrevoSettings> brevoSettings) : IRequestHandler<NotifyMemberOfLeagueApprovalCommand>
{
    private readonly BrevoSettings _brevoSettings = brevoSettings.Value;

    public async Task Handle(NotifyMemberOfLeagueApprovalCommand request, CancellationToken cancellationToken)
    {
        if (_brevoSettings.Templates == null)
            return;

        var templateId = _brevoSettings.Templates.LeagueJoinApproved;

        // 0 = the "you can now take part" template has not been configured in Brevo yet; skip sending
        // rather than calling the API with an invalid template id.
        if (templateId == 0)
            return;

        // The command carries everything the email needs (recipient, league and season names),
        // supplied by the caller which already holds the aggregates. This handler is dispatched
        // after the caller's transaction commits and performs no database access at all.
        var parameters = new
        {
            FIRST_NAME = request.MemberFirstName,
            LEAGUE_NAME = request.LeagueName,
            SEASON_NAME = request.SeasonName,
            LEAGUE_URL = BuildLeagueDashboardUrl(request.LeagueUrlBase, request.LeagueId)
        };

        await emailService.SendTemplatedEmailAsync(request.MemberEmail, templateId, parameters);
    }

    // The caller passes the request origin (e.g. https://www.thepredictions.co.uk), matching how the
    // confirmation/reset emails build their links. Fall back to the canonical site if it's missing so
    // the email's button always has a working destination.
    private static string BuildLeagueDashboardUrl(string? leagueUrlBase, int leagueId)
    {
        var baseUrl = string.IsNullOrWhiteSpace(leagueUrlBase)
            ? "https://www.thepredictions.co.uk"
            : leagueUrlBase.TrimEnd('/');

        return $"{baseUrl}/leagues/{leagueId}/dashboard";
    }
}
```

### Step 8 - JoinLeagueCommandHandler queues notifications post-commit

File: `src/ThePredictions.Application/Features/Leagues/Commands/JoinLeagueCommandHandler.cs` - replace the entire file content with (dependency changes: `IMediator` removed; `ISeasonRepository`, `IUserManager`, `IPostCommitQueue` added; `JoinLeagueCommand` itself is already `ITransactionalRequest` and needs no change):

```csharp
using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class JoinLeagueCommandHandler(
    ILeagueRepository leagueRepository,
    ISeasonRepository seasonRepository,
    ISeasonAccessService seasonAccessService,
    IUserManager userManager,
    IPostCommitQueue postCommitQueue,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<JoinLeagueCommand, int>
{
    public async Task<int> Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await FetchLeagueAsync(request, cancellationToken);

        Guard.Against.EntityNotFound(request.LeagueId ?? 0, league, "League");

        // Private leagues must be joined with their entry code. Joining by league id (the public path) is
        // rejected for private leagues so that listing them in Available Leagues never exposes a way to
        // bypass the code.
        if (request.LeagueId.HasValue && league!.EntryCode is not null)
            throw new InvalidOperationException("This league requires an entry code to join.");

        await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league!.SeasonId, cancellationToken);

        league.AddMember(request.JoiningUserId, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);
        await QueueNotificationAsync(league, request, cancellationToken);

        return league.Id;
    }

    private async Task<League?> FetchLeagueAsync(JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        if (request.LeagueId.HasValue)
            return await leagueRepository.GetByIdAsync(request.LeagueId.Value, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.EntryCode))
            return await leagueRepository.GetByEntryCodeAsync(request.EntryCode, cancellationToken);

        throw new InvalidOperationException("Either a LeagueId or an EntryCode must be provided.");
    }

    // Notifications are queued and sent by TransactionBehaviour after the join transaction commits,
    // so a rolled-back join never sends an email and the Brevo call never extends the transaction.
    // The commands carry all their data (audit 2.2), so the notify handlers do not query at all.
    private async Task QueueNotificationAsync(League league, JoinLeagueCommand request, CancellationToken cancellationToken)
    {
        var member = league.Members.FirstOrDefault(m => m.UserId == request.JoiningUserId);
        if (member is null)
            return;

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");

        // Auto-approved (the league does not require approval): tell the joiner they can take part.
        // Otherwise the request is pending: tell the admin there is someone to approve.
        if (member.Status == LeagueMemberStatus.Approved)
        {
            var joiner = await userManager.FindByIdAsync(request.JoiningUserId);
            if (joiner?.Email is null)
                return;

            postCommitQueue.Enqueue(new NotifyMemberOfLeagueApprovalCommand(
                joiner.Email,
                joiner.FirstName,
                league.Id,
                league.Name,
                season.Name,
                request.LeagueUrlBase));
        }
        else
        {
            var admin = await userManager.FindByIdAsync(league.AdministratorUserId);
            if (admin?.Email is null)
                return;

            postCommitQueue.Enqueue(new NotifyLeagueAdminOfJoinRequestCommand(
                admin.Email,
                admin.FirstName,
                league.Name,
                season.Name,
                request.JoiningUserFirstName,
                request.JoiningUserLastName,
                request.LeagueUrlBase));
        }
    }
}
```

Note: reading `[Seasons]` via `ISeasonRepository` joins the ambient transaction through `RepositoryBase` (no second connection), and `IUserManager.FindByIdAsync` reads `[AspNetUsers]`, which this transaction never locks - the deadlock the old design dodged cannot occur.

### Step 9 - UpdateLeagueCommand becomes transactional and queues its notifications

**9a.** File: `src/ThePredictions.Application/Features/Leagues/Commands/UpdateLeagueCommand.cs`

Current last line:

```csharp
    string? LeagueUrlBase = null) : IRequest;
```

Target (add the marker interface and its using):

```csharp
    string? LeagueUrlBase = null) : IRequest, ITransactionalRequest;
```

and add `using ThePredictions.Application.Common.Interfaces;` beneath `using MediatR;`.

**9b.** File: `src/ThePredictions.Application/Features/Leagues/Commands/UpdateLeagueCommandHandler.cs` - replace the entire file content with (dependency changes: `IMediator` removed; `IUserManager`, `IPostCommitQueue` added):

```csharp
using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class UpdateLeagueCommandHandler(
    ILeagueRepository leagueRepository,
    ISeasonRepository seasonRepository,
    IFieldEncryptionService fieldEncryptionService,
    IUserManager userManager,
    IPostCommitQueue postCommitQueue,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateLeagueCommand>
{
    public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, league, "League");

        if (league.AdministratorUserId != request.UserId)
            throw new UnauthorizedAccessException("Only the league administrator can update the league.");

        if (league.EntryDeadlineUtc < dateTimeProvider.UtcNow)
            throw new InvalidOperationException("This league cannot be edited because its entry deadline has passed.");

        if (league.Price != request.Price && league.Members.Count > 1)
            throw new InvalidOperationException("The entry fee cannot be changed after other players have joined the league.");

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");

        league.UpdateDetails(
            request.Name,
            request.Price,
            request.EntryDeadlineUtc,
            request.PointsForExactScore,
            request.PointsForCorrectResult,
            season,
            dateTimeProvider
        );

        league.SetBankDetails(
            fieldEncryptionService.Encrypt(NullIfBlank(request.BankAccountName)),
            fieldEncryptionService.Encrypt(NullIfBlank(request.BankSortCode)),
            fieldEncryptionService.Encrypt(NullIfBlank(request.BankAccountNumber)),
            NullIfBlank(request.PaymentReferenceTemplate));

        league.SetPrizeFundOverride(request.PrizeFundOverride);
        league.SetIsListed(request.IsListed);

        // Toggling approval off auto-approves anyone currently waiting; capture them so we can let them
        // know they can now take part.
        var autoApprovedUserIds = league.SetRequiresMemberApproval(request.RequiresMemberApproval, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);

        // Approval emails are queued and sent by TransactionBehaviour after the transaction commits,
        // carrying all their data so the notify handler does not query (audit 2.2).
        foreach (var memberUserId in autoApprovedUserIds)
        {
            var member = await userManager.FindByIdAsync(memberUserId);
            if (member?.Email is null)
                continue;

            postCommitQueue.Enqueue(new NotifyMemberOfLeagueApprovalCommand(
                member.Email,
                member.FirstName,
                league.Id,
                league.Name,
                season.Name,
                request.LeagueUrlBase));
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

### Step 10 - UpdateLeagueMemberStatusCommand becomes transactional and queues its notification

**10a.** File: `src/ThePredictions.Application/Features/Leagues/Commands/UpdateLeagueMemberStatusCommand.cs`

Current:

```csharp
using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record UpdateLeagueMemberStatusCommand(
    int LeagueId,
    string MemberId,
    string UpdatingUserId,
    LeagueMemberStatus NewStatus,
    string? LeagueUrlBase = null
) : IRequest;
```

Target:

```csharp
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record UpdateLeagueMemberStatusCommand(
    int LeagueId,
    string MemberId,
    string UpdatingUserId,
    LeagueMemberStatus NewStatus,
    string? LeagueUrlBase = null
) : IRequest, ITransactionalRequest;
```

**10b.** File: `src/ThePredictions.Application/Features/Leagues/Commands/UpdateLeagueMemberStatusCommandHandler.cs` - replace the entire file content with (dependency changes: `IMediator` removed; `ISeasonRepository`, `IUserManager`, `IPostCommitQueue` added):

```csharp
using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class UpdateLeagueMemberStatusCommandHandler(
    ILeagueRepository leagueRepository,
    ILeagueMemberRepository leagueMemberRepository,
    ISeasonRepository seasonRepository,
    IUserManager userManager,
    IPostCommitQueue postCommitQueue,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateLeagueMemberStatusCommand>
{
    public async Task Handle(UpdateLeagueMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        if (league.AdministratorUserId != request.UpdatingUserId)
            throw new UnauthorizedAccessException("Only the league administrator can update member status.");

        var member = await leagueMemberRepository.GetAsync(request.LeagueId, request.MemberId, cancellationToken);
        Guard.Against.EntityNotFound(request.MemberId, member, "LeagueMember");

        switch (request.NewStatus)
        {
            case LeagueMemberStatus.Approved:
                member.Approve(dateTimeProvider);
                break;

            case LeagueMemberStatus.Rejected:
                member.Reject();
                break;

            case LeagueMemberStatus.Pending:
                break;

            default:
                throw new InvalidOperationException("This status change is not permitted.");
        }

        await leagueMemberRepository.UpdateAsync(member, cancellationToken);

        // Let the member know they can now take part once the admin has approved them. The email is
        // queued and sent by TransactionBehaviour after the transaction commits, so a failed approval
        // never sends an email and the Brevo call never extends the transaction.
        if (request.NewStatus == LeagueMemberStatus.Approved)
            await QueueApprovalNotificationAsync(league, member.UserId, request.LeagueUrlBase, cancellationToken);
    }

    private async Task QueueApprovalNotificationAsync(League league, string memberUserId, string? leagueUrlBase, CancellationToken cancellationToken)
    {
        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");

        var member = await userManager.FindByIdAsync(memberUserId);
        if (member?.Email is null)
            return;

        postCommitQueue.Enqueue(new NotifyMemberOfLeagueApprovalCommand(
            member.Email,
            member.FirstName,
            league.Id,
            league.Name,
            season.Name,
            leagueUrlBase));
    }
}
```

### Step 11 - CreateSeasonCommandHandler dispatches the fixture sync post-commit

File: `src/ThePredictions.Application/Features/Admin/Seasons/Commands/CreateSeasonCommandHandler.cs`

**11a.** Add `IPostCommitQueue postCommitQueue,` to the primary constructor. Current constructor (lines 17-27):

```csharp
public class CreateSeasonCommandHandler(
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository,
    ITournamentRoundMappingRepository tournamentRoundMappingRepository,
    IFootballDataService footballDataService,
    IMediator mediator,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateSeasonCommandHandler> logger) : IRequestHandler<CreateSeasonCommand, SeasonDto>
```

Target:

```csharp
public class CreateSeasonCommandHandler(
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository,
    ITournamentRoundMappingRepository tournamentRoundMappingRepository,
    IFootballDataService footballDataService,
    IMediator mediator,
    IPostCommitQueue postCommitQueue,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateSeasonCommandHandler> logger) : IRequestHandler<CreateSeasonCommand, SeasonDto>
```

Keep `IMediator` - it is still used for `FetchAllTeamsQuery` inside `ValidateSeasonAgainstApiAsync`. Add `using ThePredictions.Application.Common.Interfaces;` to the file's usings.

**11b.** Replace the nested sync send. Current code (lines 46-47):

```csharp
        if (competition.ApiLeagueId.HasValue)
            await mediator.Send(new SyncSeasonWithApiCommand(createdSeason.Id), cancellationToken);
```

Target:

```csharp
        // The fixture sync is deliberately non-transactional (June 2026 audit decision 7): it makes
        // slow Football API HTTP calls and persists round by round to keep locks short. Sending it
        // nested here would pull its writes into this transaction and hold every lock across those
        // HTTP calls, so queue it to run after this transaction commits instead. The sync is
        // re-runnable and self-healing, and still completes within this HTTP request.
        if (competition.ApiLeagueId.HasValue)
            postCommitQueue.Enqueue(new SyncSeasonWithApiCommand(createdSeason.Id));
```

Behavioural consequence to be aware of (accepted): the sync now runs after the official public league is created and after the season transaction commits, instead of between the mapping step and the league creation. The sync only needs the season and rounds to exist, and `CreatePublicLeagueEntity` only uses `createdSeason`, so the reordering is safe. The returned `SeasonDto` was already built from local data. The pre-write HTTP validation calls in `ValidateSeasonAgainstApiAsync` still run inside the begun scope (after the first repository read lazily opens the connection); shortening that is out of scope.

### Step 12 - ProcessPrizesCommand becomes transactional

File: `src/ThePredictions.Application/Features/Admin/Rounds/Commands/ProcessPrizesCommand.cs`

Current:

```csharp
using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class ProcessPrizesCommand : IRequest<Unit>
{
    public int RoundId { get; init; }
    public int LeagueId { get; init; }
}
```

Target:

```csharp
using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class ProcessPrizesCommand : IRequest<Unit>, ITransactionalRequest
{
    public int RoundId { get; init; }
    public int LeagueId { get; init; }
}
```

Why this is now safe and correct: the handler's strategies delete and recreate `[Winnings]` rows for a league, which must be atomic (a failure mid-way currently leaves a league with deleted winnings and no replacements). Before this plan, adding the marker would have crashed `UpdateMatchResultsCommandHandler` at runtime ("A transaction is already active"). With the reference-counted context, the nested send from `UpdateMatchResultsCommandHandler` joins the round-completion transaction (all-or-nothing for the whole round, which is desirable), and the send from the non-transactional `RecalculateSeasonStatsCommandHandler` gets its own per-league transaction (the right granularity for a long repair loop). Do NOT convert the class to a record or change `IRequest<Unit>` here - that is audit item 4.5, out of scope.

### Step 13 - Document why SyncSeasonWithApiCommand must stay non-transactional (audit 2.7)

File: `src/ThePredictions.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs`

Current code (lines 24-25):

```csharp
    public async Task Handle(SyncSeasonWithApiCommand request, CancellationToken cancellationToken)
    {
        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
```

Target:

```csharp
    public async Task Handle(SyncSeasonWithApiCommand request, CancellationToken cancellationToken)
    {
        // Deliberately NOT ITransactionalRequest (June 2026 audit decision 7): this handler calls the
        // slow external Football API and persists round by round so database locks stay short. Wrapping
        // it in a single transaction would hold locks across those HTTP calls. It is re-runnable and
        // self-healing, so partial completion is safe. Do not add the marker interface.
        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
```

### Step 14 - Per-command ITransactionalRequest decisions (summary of what this plan changes and deliberately does not)

| Command | Today | Decision | Rationale |
|---|---|---|---|
| `ProcessPrizesCommand` | No | **Make transactional** (step 12) | Delete-and-recreate of winnings must be atomic; joins the round transaction when nested, own transaction when run from the recalculate loop |
| `UpdateLeagueCommand` | No | **Make transactional** (step 9) | League update plus N member auto-approval writes are multi-statement; also required for the post-commit queue guard |
| `UpdateLeagueMemberStatusCommand` | No | **Make transactional** (step 10) | Member write plus queued notification; `LeagueRepository`/`LeagueMemberRepository` writes are multi-statement; required for the queue guard |
| `RecalculateSeasonStatsCommand` | No | **Keep non-transactional** | Long-running admin repair loop over every completed round; one giant transaction would hold locks for the whole recalculation. It is idempotent and re-runnable; atomicity now exists per league via the nested transactional `ProcessPrizesCommand` |
| `SyncSeasonWithApiCommand` | No | **Keep non-transactional** (decision 7, step 13) | Short locks during slow external HTTP calls; re-runnable and self-healing. MUST NOT gain the marker |
| `PublishUpcomingRoundsCommand` | No | **Keep non-transactional** | Sent nested from inside `SyncSeasonWithApiCommandHandler` (lines 293 and 490); making it transactional would open a transaction in the middle of the deliberately short-lock sync. Each round publish is an independent idempotent write; partial completion self-heals on the next daily run |
| `LogoutCommand` | Yes | **Keep as-is** | Single read plus single write; the transaction is unnecessary but harmless, and removing the marker is churn with no benefit |
| `Notify*` commands | No | **Keep non-transactional** | After step 7 they perform no database access at all |

### Step 15 - Update existing tests

All in `tests/Unit/ThePredictions.Application.Tests.Unit`.

**15a.** `Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandlerTests.cs` - replace the entire file content with:

```csharp
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class NotifyLeagueAdminOfJoinRequestCommandHandlerTests
{
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly NotifyLeagueAdminOfJoinRequestCommandHandler _handler;

    private readonly BrevoSettings _brevoSettings = new()
    {
        Templates = new TemplateSettings
        {
            JoinLeagueRequest = 300
        }
    };

    public NotifyLeagueAdminOfJoinRequestCommandHandlerTests()
    {
        var options = Options.Create(_brevoSettings);
        _handler = new NotifyLeagueAdminOfJoinRequestCommandHandler(_emailService, options);
    }

    [Fact]
    public async Task Handle_ShouldSendEmailToSuppliedAdminAddress_WhenTemplateIsConfigured()
    {
        // Arrange - the command carries all the data; the handler performs no queries
        var command = new NotifyLeagueAdminOfJoinRequestCommand(
            "admin@example.com", "Admin", "Test League", "2025/26", "Jane", "Doe");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendTemplatedEmailAsync(
            "admin@example.com", 300, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenTemplatesNotConfigured()
    {
        // Arrange
        var settingsWithoutTemplates = new BrevoSettings { Templates = null };
        var options = Options.Create(settingsWithoutTemplates);
        var handler = new NotifyLeagueAdminOfJoinRequestCommandHandler(_emailService, options);
        var command = new NotifyLeagueAdminOfJoinRequestCommand(
            "admin@example.com", "Admin", "Test League", "2025/26", "Jane", "Doe");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }
}
```

The old `Handle_ShouldNotSendEmail_WhenAdminNotFound` test is deleted deliberately: there is no lookup any more, which is the whole point of audit 2.2. The "notify handlers no longer query" requirement is enforced at compile time - the constructor no longer accepts `IApplicationReadDbConnection`, so a query cannot be reintroduced without changing the signature.

**15b.** `Features/Leagues/Commands/JoinLeagueCommandHandlerTests.cs` - replace the entire file content with:

```csharp
using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class JoinLeagueCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly ISeasonAccessService _seasonAccessService = Substitute.For<ISeasonAccessService>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IPostCommitQueue _postCommitQueue = Substitute.For<IPostCommitQueue>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly JoinLeagueCommandHandler _handler;

    public JoinLeagueCommandHandlerTests()
    {
        _handler = new JoinLeagueCommandHandler(
            _leagueRepository, _seasonRepository, _seasonAccessService, _userManager, _postCommitQueue, _dateTimeProvider);

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSeason());
        _userManager.FindByIdAsync("admin-user").Returns(new ApplicationUser
        {
            Id = "admin-user",
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User"
        });
        _userManager.FindByIdAsync("new-user").Returns(new ApplicationUser
        {
            Id = "new-user",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe"
        });
    }

    private Season CreateSeason(int id = 1) =>
        new(id: id, name: "2025/26",
            startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8),
            isActive: true, numberOfRounds: 38, competitionId: 1,
            passStandardPrice: null, passPremiumPrice: null);

    private League CreateLeague(int id = 1, string administratorUserId = "admin-user", DateTime? entryDeadlineUtc = null, string? entryCode = null, bool requiresMemberApproval = true)
    {
        return new League(
            id: id, name: "Test League", seasonId: 1,
            administratorUserId: administratorUserId,
            entryCode: entryCode,
            createdAtUtc: _dateTimeProvider.UtcNow.AddDays(-30),
            entryDeadlineUtc: entryDeadlineUtc ?? _dateTimeProvider.UtcNow.AddMonths(1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null,
            requiresMemberApproval: requiresMemberApproval);
    }

    [Fact]
    public async Task Handle_ShouldAddMemberAndUpdateLeague_WhenJoiningByLeagueId()
    {
        // Arrange
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAddMemberAndUpdateLeague_WhenJoiningByEntryCode()
    {
        // Arrange
        var league = CreateLeague(id: 5, entryCode: "ABC123");
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: null, EntryCode: "ABC123");

        _leagueRepository.GetByEntryCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFoundById()
    {
        // Arrange
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 999, EntryCode: null);

        _leagueRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenLeagueNotFoundByEntryCode()
    {
        // Arrange
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: null, EntryCode: "INVALID");

        _leagueRepository.GetByEntryCodeAsync("INVALID", Arg.Any<CancellationToken>())
            .Returns((League?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenNoLeagueIdOrEntryCode()
    {
        // Arrange
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: null, EntryCode: null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LeagueId*EntryCode*");
    }

    [Fact]
    public async Task Handle_ShouldQueueAdminNotificationWithCarriedData_WhenJoinRequiresApproval()
    {
        // Arrange
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - queued for post-commit dispatch, carrying recipient and season data (audit 2.2)
        _postCommitQueue.Received(1).Enqueue(
            Arg.Is<NotifyLeagueAdminOfJoinRequestCommand>(n =>
                n.AdminEmail == "admin@example.com" &&
                n.AdminFirstName == "Admin" &&
                n.LeagueName == "Test League" &&
                n.SeasonName == "2025/26" &&
                n.NewMemberFirstName == "Jane" &&
                n.NewMemberLastName == "Doe"));
    }

    [Fact]
    public async Task Handle_ShouldPreferLeagueId_WhenBothLeagueIdAndEntryCodeProvided()
    {
        // Arrange
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: "ABC123");

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).GetByIdAsync(5, Arg.Any<CancellationToken>());
        await _leagueRepository.DidNotReceive().GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenJoiningPrivateLeagueByLeagueId()
    {
        // Arrange - listing a private league exposes its id, so the by-id path must reject it (code required)
        var league = CreateLeague(id: 5, entryCode: "ABC123");
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entry code*");
        await _leagueRepository.DidNotReceive().UpdateAsync(Arg.Any<League>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldQueueMemberNotificationWithCarriedData_WhenLeagueDoesNotRequireApproval()
    {
        // Arrange - approval off: the joiner is auto-approved and told they can take part
        var league = CreateLeague(id: 5, requiresMemberApproval: false);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null, LeagueUrlBase: "https://www.thepredictions.co.uk");

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _postCommitQueue.Received(1).Enqueue(
            Arg.Is<NotifyMemberOfLeagueApprovalCommand>(n =>
                n.MemberEmail == "jane@example.com" &&
                n.MemberFirstName == "Jane" &&
                n.LeagueId == 5 &&
                n.LeagueName == "Test League" &&
                n.SeasonName == "2025/26" &&
                n.LeagueUrlBase == "https://www.thepredictions.co.uk"));

        _postCommitQueue.DidNotReceive().Enqueue(Arg.Any<NotifyLeagueAdminOfJoinRequestCommand>());
    }

    [Fact]
    public async Task Handle_ShouldNotQueueNotification_WhenRecipientUserHasNoEmail()
    {
        // Arrange - a missing user/email must not fail the join; it just skips the email
        var league = CreateLeague(id: 5);
        var command = new JoinLeagueCommand("new-user", "Jane", "Doe", LeagueId: 5, EntryCode: null);

        _leagueRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(league);
        _userManager.FindByIdAsync("admin-user").Returns((ApplicationUser?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
        _postCommitQueue.DidNotReceive().Enqueue(Arg.Any<object>());
    }
}
```

Note: the `Season` public hydration constructor parameters above mirror the ones already used in `UpdateLeagueCommandHandlerTests.CreateSeason`; if the constructor differs when you read it, copy the shape from that existing test file.

**15c.** `Features/Leagues/Commands/UpdateLeagueCommandHandlerTests.cs` - three targeted edits:

1. Replace the fields/constructor block. Current:

```csharp
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly UpdateLeagueCommandHandler _handler;

    public UpdateLeagueCommandHandlerTests()
    {
        _handler = new UpdateLeagueCommandHandler(_leagueRepository, _seasonRepository, _fieldEncryptionService, _mediator, _dateTimeProvider);
    }
```

Target:

```csharp
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IPostCommitQueue _postCommitQueue = Substitute.For<IPostCommitQueue>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly UpdateLeagueCommandHandler _handler;

    public UpdateLeagueCommandHandlerTests()
    {
        _handler = new UpdateLeagueCommandHandler(
            _leagueRepository, _seasonRepository, _fieldEncryptionService, _userManager, _postCommitQueue, _dateTimeProvider);
    }
```

2. Fix the usings: remove `using MediatR;` and add `using ThePredictions.Application.Common.Interfaces;` (keep `using ThePredictions.Application.Services;`, which now covers `IUserManager`).

3. Append this new test before the class's closing brace (the `CreateLeague` helper already accepts `members`; pass `requiresMemberApproval: true` through a new optional parameter on the helper, defaulting to `true`, mirroring the pattern in `JoinLeagueCommandHandlerTests.CreateLeague`):

```csharp
    [Fact]
    public async Task Handle_ShouldQueueApprovalNotifications_WhenTogglingApprovalOffAutoApprovesPendingMembers()
    {
        // Arrange - a pending member exists and the admin turns approval off
        var members = new List<LeagueMember?>
        {
            new(leagueId: 1, userId: "pending-user", status: LeagueMemberStatus.Pending,
                isAlertDismissed: false, isArchivedByUser: false, joinedAtUtc: _dateTimeProvider.UtcNow.AddDays(-5),
                approvedAtUtc: null, roundResults: null)
        };
        var league = CreateLeague(administratorUserId: "admin-user", members: members);
        var season = CreateSeason();
        var command = new UpdateLeagueCommand(1, "Updated", 0m,
            _dateTimeProvider.UtcNow.AddMonths(1), 3, 1, "admin-user",
            RequiresMemberApproval: false, LeagueUrlBase: "https://www.thepredictions.co.uk");

        _leagueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(league);
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(season);
        _userManager.FindByIdAsync("pending-user").Returns(new ApplicationUser
        {
            Id = "pending-user",
            Email = "pending@example.com",
            FirstName = "Penny",
            LastName = "Ding"
        });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _postCommitQueue.Received(1).Enqueue(
            Arg.Is<NotifyMemberOfLeagueApprovalCommand>(n =>
                n.MemberEmail == "pending@example.com" &&
                n.MemberFirstName == "Penny" &&
                n.SeasonName == "2025/26"));
    }
```

If `CreateLeague` in this file does not currently expose `requiresMemberApproval`, either add the optional parameter (defaulting to the League constructor's current default) or construct the league inline for this one test; check `League`'s hydration constructor signature and match it exactly.

**15d.** `Features/Leagues/Commands/UpdateLeagueMemberStatusCommandHandlerTests.cs` - three targeted edits:

1. Replace the fields/constructor block. Current:

```csharp
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ILeagueMemberRepository _leagueMemberRepository = Substitute.For<ILeagueMemberRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly UpdateLeagueMemberStatusCommandHandler _handler;

    public UpdateLeagueMemberStatusCommandHandlerTests()
    {
        _handler = new UpdateLeagueMemberStatusCommandHandler(
            _leagueRepository, _leagueMemberRepository, _mediator, _dateTimeProvider);
    }
```

Target (also seed the season and member-user stubs every approval test needs):

```csharp
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ILeagueMemberRepository _leagueMemberRepository = Substitute.For<ILeagueMemberRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IPostCommitQueue _postCommitQueue = Substitute.For<IPostCommitQueue>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly UpdateLeagueMemberStatusCommandHandler _handler;

    public UpdateLeagueMemberStatusCommandHandlerTests()
    {
        _handler = new UpdateLeagueMemberStatusCommandHandler(
            _leagueRepository, _leagueMemberRepository, _seasonRepository, _userManager, _postCommitQueue, _dateTimeProvider);

        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(
            new Season(id: 1, name: "2025/26",
                startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
                endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8),
                isActive: true, numberOfRounds: 38, competitionId: 1,
                passStandardPrice: null, passPremiumPrice: null));
        _userManager.FindByIdAsync("member-1").Returns(new ApplicationUser
        {
            Id = "member-1",
            Email = "member1@example.com",
            FirstName = "Mia",
            LastName = "Member"
        });
    }
```

2. Fix the usings: remove `using MediatR;`; add `using ThePredictions.Application.Common.Interfaces;` and `using ThePredictions.Application.Services;`.

3. In `Handle_ShouldApproveMember_WhenAdministratorApprovesAndMemberIsPending`, replace the mediator assertion. Current:

```csharp
        await _mediator.Received(1).Send(
            Arg.Is<NotifyMemberOfLeagueApprovalCommand>(n => n.MemberUserId == "member-1" && n.LeagueName == "Test League" && n.SeasonId == 1),
            Arg.Any<CancellationToken>());
```

Target:

```csharp
        _postCommitQueue.Received(1).Enqueue(
            Arg.Is<NotifyMemberOfLeagueApprovalCommand>(n =>
                n.MemberEmail == "member1@example.com" &&
                n.MemberFirstName == "Mia" &&
                n.LeagueName == "Test League" &&
                n.SeasonName == "2025/26"));
```

Also add this assertion to `Handle_ShouldRejectMember_WhenAdministratorRejects` (after the existing asserts) to pin the negative path:

```csharp
        _postCommitQueue.DidNotReceive().Enqueue(Arg.Any<object>());
```

### Step 16 - New unit tests: TransactionBehaviour

New file: `tests/Unit/ThePredictions.Application.Tests.Unit/Common/Behaviours/TransactionBehaviourTests.cs`

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Behaviours;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Data;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Behaviours;

public class TransactionBehaviourTests
{
    private record TestTransactionalCommand : IRequest<int>, ITransactionalRequest;

    private readonly IDbTransactionContext _transactionContext = Substitute.For<IDbTransactionContext>();
    private readonly IPostCommitQueue _postCommitQueue = Substitute.For<IPostCommitQueue>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<TransactionBehaviour<TestTransactionalCommand, int>> _logger =
        Substitute.For<ILogger<TransactionBehaviour<TestTransactionalCommand, int>>>();
    private readonly TransactionBehaviour<TestTransactionalCommand, int> _behaviour;
    private readonly TestTransactionalCommand _request = new();

    public TransactionBehaviourTests()
    {
        _behaviour = new TransactionBehaviour<TestTransactionalCommand, int>(
            _transactionContext, _postCommitQueue, _mediator, _logger);

        _postCommitQueue.Drain().Returns([]);
    }

    [Fact]
    public async Task Handle_ShouldBeginCommitAndReturnResponse_WhenHandlerSucceeds()
    {
        // Arrange
        RequestHandlerDelegate<int> next = _ => Task.FromResult(42);

        // Act
        var result = await _behaviour.Handle(_request, next, CancellationToken.None);

        // Assert
        result.Should().Be(42);
        await _transactionContext.Received(1).BeginAsync(Arg.Any<CancellationToken>());
        await _transactionContext.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _transactionContext.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDispatchQueuedCommands_WhenOutermostTransactionCommits()
    {
        // Arrange
        var queuedCommand = new object();
        _transactionContext.HasActiveTransaction.Returns(false);
        _postCommitQueue.Drain().Returns([queuedCommand]);
        RequestHandlerDelegate<int> next = _ => Task.FromResult(1);

        // Act
        await _behaviour.Handle(_request, next, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(queuedCommand, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotDispatchQueuedCommands_WhenNestedTransactionIsStillActive()
    {
        // Arrange - after an inner commit the outer transaction is still active
        _transactionContext.HasActiveTransaction.Returns(true);
        RequestHandlerDelegate<int> next = _ => Task.FromResult(1);

        // Act
        await _behaviour.Handle(_request, next, CancellationToken.None);

        // Assert
        _postCommitQueue.DidNotReceive().Drain();
        await _mediator.DidNotReceive().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRollbackClearQueueAndRethrow_WhenHandlerThrows()
    {
        // Arrange
        _transactionContext.HasActiveTransaction.Returns(false);
        RequestHandlerDelegate<int> next = _ => throw new InvalidOperationException("handler failed");

        // Act
        var act = () => _behaviour.Handle(_request, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("handler failed");
        await _transactionContext.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        _postCommitQueue.Received(1).Clear();
        await _mediator.DidNotReceive().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotClearQueue_WhenNestedRollbackLeavesOuterTransactionActive()
    {
        // Arrange - an inner scope failing must leave the queue for the outer scope to clear
        _transactionContext.HasActiveTransaction.Returns(true);
        RequestHandlerDelegate<int> next = _ => throw new InvalidOperationException("inner failure");

        // Act
        var act = () => _behaviour.Handle(_request, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _transactionContext.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        _postCommitQueue.DidNotReceive().Clear();
    }

    [Fact]
    public async Task Handle_ShouldNotFailRequest_WhenPostCommitCommandThrows()
    {
        // Arrange - the transaction has committed; a failing email must not turn success into a 500
        var queuedCommand = new object();
        _transactionContext.HasActiveTransaction.Returns(false);
        _postCommitQueue.Drain().Returns([queuedCommand]);
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns<object?>(_ => throw new InvalidOperationException("brevo down"));
        RequestHandlerDelegate<int> next = _ => Task.FromResult(7);

        // Act
        var result = await _behaviour.Handle(_request, next, CancellationToken.None);

        // Assert
        result.Should().Be(7);
    }
}
```

(MediatR 14's `RequestHandlerDelegate<TResponse>` takes a `CancellationToken`, hence the `_ =>` lambdas. The nested private record is allowed - the one-type-per-file rule concerns public types.)

### Step 17 - New unit tests: PostCommitQueue

New file: `tests/Unit/ThePredictions.Application.Tests.Unit/Common/PostCommitQueueTests.cs`

```csharp
using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common;
using ThePredictions.Application.Data;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common;

public class PostCommitQueueTests
{
    private readonly IDbTransactionContext _transactionContext = Substitute.For<IDbTransactionContext>();
    private readonly PostCommitQueue _queue;

    public PostCommitQueueTests()
    {
        _queue = new PostCommitQueue(_transactionContext);
    }

    [Fact]
    public void Enqueue_ShouldThrowInvalidOperationException_WhenNoTransactionIsActive()
    {
        // Arrange
        _transactionContext.HasActiveTransaction.Returns(false);

        // Act
        var act = () => _queue.Enqueue(new object());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ITransactionalRequest*");
    }

    [Fact]
    public void Enqueue_ShouldQueueCommand_WhenTransactionIsActive()
    {
        // Arrange
        _transactionContext.HasActiveTransaction.Returns(true);
        var command = new object();

        // Act
        _queue.Enqueue(command);

        // Assert
        _queue.Drain().Should().ContainSingle().Which.Should().BeSameAs(command);
    }

    [Fact]
    public void Drain_ShouldReturnCommandsInOrderAndEmptyTheQueue_WhenCommandsAreQueued()
    {
        // Arrange
        _transactionContext.HasActiveTransaction.Returns(true);
        var first = new object();
        var second = new object();
        _queue.Enqueue(first);
        _queue.Enqueue(second);

        // Act
        var drained = _queue.Drain();

        // Assert
        drained.Should().ContainInOrder(first, second);
        _queue.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Clear_ShouldRemoveAllQueuedCommands_WhenCalled()
    {
        // Arrange
        _transactionContext.HasActiveTransaction.Returns(true);
        _queue.Enqueue(new object());

        // Act
        _queue.Clear();

        // Assert
        _queue.Drain().Should().BeEmpty();
    }
}
```

### Step 18 - New unit tests: NotifyMemberOfLeagueApprovalCommandHandler (currently untested)

New file: `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommandHandlerTests.cs`

```csharp
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

public class NotifyMemberOfLeagueApprovalCommandHandlerTests
{
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();

    private static NotifyMemberOfLeagueApprovalCommand CreateCommand() =>
        new("member@example.com", "Mia", 5, "Test League", "2025/26", "https://www.thepredictions.co.uk");

    private NotifyMemberOfLeagueApprovalCommandHandler CreateHandler(BrevoSettings settings) =>
        new(_emailService, Options.Create(settings));

    [Fact]
    public async Task Handle_ShouldSendEmailToSuppliedMemberAddress_WhenTemplateIsConfigured()
    {
        // Arrange
        var handler = CreateHandler(new BrevoSettings { Templates = new TemplateSettings { LeagueJoinApproved = 400 } });

        // Act
        await handler.Handle(CreateCommand(), CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendTemplatedEmailAsync(
            "member@example.com", 400, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenTemplateIdIsNotConfigured()
    {
        // Arrange - 0 means the Brevo template has not been set up yet
        var handler = CreateHandler(new BrevoSettings { Templates = new TemplateSettings { LeagueJoinApproved = 0 } });

        // Act
        await handler.Handle(CreateCommand(), CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenTemplatesNotConfigured()
    {
        // Arrange
        var handler = CreateHandler(new BrevoSettings { Templates = null });

        // Act
        await handler.Handle(CreateCommand(), CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendTemplatedEmailAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>());
    }
}
```

(Check `TemplateSettings` for the exact property name `LeagueJoinApproved` - it is read in the current handler at `NotifyMemberOfLeagueApprovalCommandHandler.cs` line 18.)

### Step 19 - New unit tests: DbTransactionContext (in the Composition test project)

`DbTransactionContext` lives in Infrastructure; there is no Infrastructure unit test project, but `tests/Unit/ThePredictions.Composition.Tests.Unit` already references `ThePredictions.Infrastructure`, so the tests go there.

**19a.** Add NSubstitute to `tests/Unit/ThePredictions.Composition.Tests.Unit/ThePredictions.Composition.Tests.Unit.csproj`. In the first `<ItemGroup>` (after the `Microsoft.AspNetCore.DataProtection` reference), add:

```xml
    <PackageReference Include="NSubstitute" Version="5.3.0" />
```

(Pinned to match the version already used by the Application test project, keeping the solution on one NSubstitute version.)

**19b.** New file: `tests/Unit/ThePredictions.Composition.Tests.Unit/DbTransactionContextTests.cs`

```csharp
using System.Data;
using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Infrastructure.Data;
using Xunit;

namespace ThePredictions.Composition.Tests.Unit;

public class DbTransactionContextTests
{
    private readonly IDbConnectionFactory _connectionFactory = Substitute.For<IDbConnectionFactory>();
    private readonly IDbConnection _connection = Substitute.For<IDbConnection>();
    private readonly IDbTransaction _transaction = Substitute.For<IDbTransaction>();
    private readonly DbTransactionContext _context;

    public DbTransactionContextTests()
    {
        _connectionFactory.CreateConnection().Returns(_connection);
        _connection.BeginTransaction().Returns(_transaction);
        _context = new DbTransactionContext(_connectionFactory);
    }

    [Fact]
    public void Connection_ShouldThrowInvalidOperationException_WhenNoTransactionHasBegun()
    {
        // Act
        var act = () => _context.Connection;

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*BeginAsync*");
    }

    [Fact]
    public async Task BeginAsync_ShouldJoinOuterTransaction_WhenCalledNested()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);

        // Act
        var act = () => _context.BeginAsync(CancellationToken.None);

        // Assert - nested begin no longer throws and only one physical connection is used
        await act.Should().NotThrowAsync();
        _ = _context.Connection;
        _connectionFactory.Received(1).CreateConnection();
        _context.HasActiveTransaction.Should().BeTrue();
    }

    [Fact]
    public async Task CommitAsync_ShouldNotCommitUnderlyingTransaction_WhenNestedScopeCommits()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;

        // Act - the inner scope commits; the outer scope is still active
        await _context.CommitAsync(CancellationToken.None);

        // Assert
        _transaction.DidNotReceive().Commit();
        _context.HasActiveTransaction.Should().BeTrue();
    }

    [Fact]
    public async Task CommitAsync_ShouldCommitUnderlyingTransactionOnce_WhenOutermostScopeCommits()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;

        // Act
        await _context.CommitAsync(CancellationToken.None);
        await _context.CommitAsync(CancellationToken.None);

        // Assert
        _transaction.Received(1).Commit();
        _context.HasActiveTransaction.Should().BeFalse();
    }

    [Fact]
    public async Task CommitAsync_ShouldThrowInvalidOperationException_WhenNoTransactionIsActive()
    {
        // Act
        var act = () => _context.CommitAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BeginAsync*");
    }

    [Fact]
    public async Task RollbackAsync_ShouldRollBackUnderlyingTransactionImmediately_WhenNestedScopeRollsBack()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;

        // Act - the inner scope fails and rolls back
        await _context.RollbackAsync(CancellationToken.None);

        // Assert - the real rollback happens once, at the first rollback
        _transaction.Received(1).Rollback();
        _context.HasActiveTransaction.Should().BeTrue();
    }

    [Fact]
    public async Task RollbackAsync_ShouldRollBackOnlyOnce_WhenInnerAndOuterScopesBothRollBack()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;

        // Act
        await _context.RollbackAsync(CancellationToken.None);
        await _context.RollbackAsync(CancellationToken.None);

        // Assert
        _transaction.Received(1).Rollback();
        _context.HasActiveTransaction.Should().BeFalse();
    }

    [Fact]
    public async Task CommitAsync_ShouldThrowInvalidOperationException_WhenInnerScopeHasRolledBack()
    {
        // Arrange - inner rollback dooms the outer scope; a swallowed inner exception must not commit
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;
        await _context.RollbackAsync(CancellationToken.None);

        // Act
        var act = () => _context.CommitAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rolled back*");
    }

    [Fact]
    public async Task Connection_ShouldThrowInvalidOperationException_WhenTransactionHasRolledBack()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;
        await _context.RollbackAsync(CancellationToken.None);

        // Act
        var act = () => _context.Connection;

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*rolled back*");
    }

    [Fact]
    public async Task BeginAsync_ShouldThrowInvalidOperationException_WhenRolledBackScopeHasNotFullyUnwound()
    {
        // Arrange
        await _context.BeginAsync(CancellationToken.None);
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;
        await _context.RollbackAsync(CancellationToken.None);

        // Act - starting a new nested scope while the doomed outer frame is still unwinding
        var act = () => _context.BeginAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rolled back*");
    }

    [Fact]
    public async Task BeginAsync_ShouldStartFreshTransaction_WhenPreviousTransactionFullyUnwound()
    {
        // Arrange - a failed transaction that has fully unwound must not poison the DI scope
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;
        await _context.RollbackAsync(CancellationToken.None);

        // Act
        await _context.BeginAsync(CancellationToken.None);
        _ = _context.Connection;

        // Assert - a second physical connection and transaction are created
        _connectionFactory.Received(2).CreateConnection();
        _context.HasActiveTransaction.Should().BeTrue();
    }

    [Fact]
    public async Task CommitAsync_ShouldNotThrow_WhenNoConnectionWasEverOpened()
    {
        // Arrange - a transactional handler that never touched a repository
        await _context.BeginAsync(CancellationToken.None);

        // Act
        var act = () => _context.CommitAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _context.HasActiveTransaction.Should().BeFalse();
    }
}
```

### Step 20 - Build and test

Run, in order, from the repository root:

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test tests/Unit/ThePredictions.Application.Tests.Unit/ThePredictions.Application.Tests.Unit.csproj
dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit/ThePredictions.Composition.Tests.Unit.csproj
dotnet test tests/Unit/ThePredictions.Domain.Tests.Unit/ThePredictions.Domain.Tests.Unit.csproj
```

All must pass with zero warnings. No Domain code changes, so the coverage gate is unaffected; run `tools\Test Coverage\coverage-unit.bat` anyway and confirm the Domain report still shows 100% line and 100% branch.

---

## Out of scope

- **Digest and prize emails inside the round-completion transaction.** `UpdateMatchResultsCommandHandler` still sends `SendRoundDigestEmailsCommand` and `SendPrizeNotificationsCommand` nested inside its transaction (lines 113 and 117). Both are idempotent via sent-logs and could move to the post-commit queue with the mechanism this plan introduces, but they carry heavier data-gathering logic and deserve their own change. Same for the emails sent by the transactional `SendScheduledRemindersCommand`.
- **Shortening `CreateSeasonCommand`'s transaction around its pre-write HTTP validation** (`ValidateSeasonAgainstApiAsync` runs after the first repository read has lazily opened the connection). Low value: the transaction is idle and holds no interesting locks at that point.
- **Audit 4.5 shape clean-ups** for `ProcessPrizesCommand` (class to record, `IRequest<Unit>` to `IRequest`) and audit 1.3 (strategy runs once per prize setting instead of once per prize type).
- **Audit 2.1** (repositories in query handlers), **audit 1.1** (server-side validation) and the other audit items not listed in this plan's scope.
- **A `TransactionScope`/savepoint-based design** - rejected in the Design section, not deferred.
- **Database schema changes** - there are none; `docs/guides/database-schema.md` and the DatabaseTools refresh tool need no updates.

## Verification checklist

- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` succeeds with zero warnings (xUnit1051 included).
- [ ] `dotnet test tests/Unit/ThePredictions.Application.Tests.Unit/ThePredictions.Application.Tests.Unit.csproj` passes, including the new `TransactionBehaviourTests`, `PostCommitQueueTests`, `NotifyMemberOfLeagueApprovalCommandHandlerTests` and the updated Join/Update/Notify tests.
- [ ] `dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit/ThePredictions.Composition.Tests.Unit.csproj` passes: `ContainerValidationTests` proves `IPostCommitQueue` and the four-dependency `TransactionBehaviour` resolve, and the new `DbTransactionContextTests` all pass.
- [ ] `dotnet test tests/Unit/ThePredictions.Domain.Tests.Unit/ThePredictions.Domain.Tests.Unit.csproj` passes and `tools\Test Coverage\coverage-unit.bat` still reports 100% line and 100% branch for Domain (no Domain code was changed).
- [ ] `grep -rn "IApplicationReadDbConnection" src/ThePredictions.Application/Features/Leagues/Commands/` returns nothing (CQRS rule 1 restored for the notify handlers).
- [ ] `grep -rn "LeagueAdminDto\|LeagueMemberContactDto" src/ tests/` returns nothing (co-located DTOs deleted, one-type-per-file restored).
- [ ] `grep -n "ITransactionalRequest" src/ThePredictions.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommand.cs` returns nothing (decision 7 respected) and the explanatory comment exists at the top of `SyncSeasonWithApiCommandHandler.Handle`.
- [ ] `ProcessPrizesCommand`, `UpdateLeagueCommand` and `UpdateLeagueMemberStatusCommand` implement `ITransactionalRequest`; `RecalculateSeasonStatsCommand` and `PublishUpcomingRoundsCommand` do not.
- [ ] `DbTransactionContext` contains no `_begun` field, `BeginAsync` no longer throws "A transaction is already active.", and `IDbTransactionContext` declares `RollbackAsync`.
- [ ] `TransactionBehaviour`'s catch block calls `RollbackAsync` and clears the queue; success path dispatches queued commands only when `HasActiveTransaction` is false after commit.
- [ ] No em dashes or en dashes and no US English spellings were introduced in any changed file.
- [ ] Manual smoke (optional but recommended): run the API locally, join a league requiring approval, and confirm the admin email still arrives and the member row is committed even if Brevo is unreachable (the join must succeed; the email failure is logged).
