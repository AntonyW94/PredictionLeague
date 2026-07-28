# Application Layer Infrastructure Leaks

## Status

**Not Started** | In Progress | Complete

## Priority

**Medium** - No user-facing bug today, but vendor knowledge (Brevo template ids), cryptography, and provider wire-format semantics (api-sports.io status strings) all live in `ThePredictions.Application`, and the "email template not configured" guard is re-implemented in four inconsistent ways. Every new email or score-sync feature copies one of these patterns, so the cost compounds. The June 2026 review already lists the `IEmailService` CancellationToken gap (item 4.9 in `docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md`, line 205).

## Summary

Four verified leaks of infrastructure concerns into `src/ThePredictions.Application`, each with a self-contained work package:

- **A.** Move the AES-GCM `FieldEncryptionService` implementation to Infrastructure (interface stays in Application).
- **B.** Add `CancellationToken` to `IEmailService` and thread it through all call sites (and record the decision NOT to add tokens to `IUserManager`).
- **C.** Replace per-handler Brevo template-id lookups with an `IEmailTemplateResolver` abstraction that applies one consistent missing-template policy.
- **D.** Stop Application code interpreting raw api-sports.io status strings; have Infrastructure normalise them to an enum.

Do the packages in the order A, B, C, D. Each package leaves the solution building and all tests green, so they can also land as separate commits/PRs.

All file paths below are relative to the repository root. All line numbers were verified on 2026-07-06; re-verify before editing and follow the code where it has drifted.

---

## Work package A: move FieldEncryptionService to Infrastructure

### Current state (verified)

- `src/ThePredictions.Application/Services/FieldEncryptionService.cs` is a complete AES-GCM implementation (nonce/tag layout, key validation, versioned ciphertext format). Cryptography is an infrastructure concern.
- The interface `src/ThePredictions.Application/Services/IFieldEncryptionService.cs` is correctly placed in Application and stays there.
- Registration is in `src/ThePredictions.Infrastructure/DependencyInjection.cs` lines 36-38:

```csharp
services.Configure<FieldEncryptionSettings>(
    configuration.GetSection(FieldEncryptionSettings.SectionName));
services.AddSingleton<IFieldEncryptionService, FieldEncryptionService>();
```

- Consumers (all inject the interface only, no change needed): `SetPayoutDetailsCommandHandler`, `GetMyPayoutDetailsQueryHandler`, `UpdateLeagueCommandHandler`, `CreateLeagueCommandHandler`, `GetLeaguePaymentInfoQueryHandler`, `GetLeagueBankDetailsQueryHandler`, `GetLeaguePayoutsQueryHandler` (all under `src/ThePredictions.Application/Features/...`).
- Tests: `tests/Unit/ThePredictions.Application.Tests.Unit/Services/FieldEncryptionServiceTests.cs` instantiates the concrete `FieldEncryptionService` directly. The test project (`tests/Unit/ThePredictions.Application.Tests.Unit/ThePredictions.Application.Tests.Unit.csproj`) references ONLY `ThePredictions.Application` and `ThePredictions.Tests.Shared` - it does not reference Infrastructure, so the test file cannot stay there after the move.
- There is no Infrastructure unit-test project (verified: only `Application`, `Composition`, `Domain`, `Validators`, `Web.Client` test projects exist under `tests/Unit/`).

### Test relocation decision

Move `FieldEncryptionServiceTests.cs` to `tests/Unit/ThePredictions.Composition.Tests.Unit/Services/FieldEncryptionServiceTests.cs`. Justification: the Composition test project already references `ThePredictions.Infrastructure` (see its `.csproj`, ProjectReference on line 25), so no new inter-project references are introduced, and the tests keep running in every `dotnet test` sweep. This is a documented temporary home: when the test-suite plan (`docs/todo/architecture/test-suite/README.md`) creates a `ThePredictions.Infrastructure.Tests.Unit` project, relocate the file there. Do NOT add an Infrastructure reference to the Application test project; that would blur its layering for one file.

### Steps

1. Move `src/ThePredictions.Application/Services/FieldEncryptionService.cs` to `src/ThePredictions.Infrastructure/Services/FieldEncryptionService.cs` (the `Services` folder already holds `BrevoEmailService`, `FootballDataService`, `UserManagerService`, etc.).
2. In the moved file, change the namespace from `ThePredictions.Application.Services` to `ThePredictions.Infrastructure.Services` and add `using ThePredictions.Application.Services;` (for `IFieldEncryptionService`). Keep `using ThePredictions.Application.Configuration;` (for `FieldEncryptionSettings`, which stays in Application - it is a logic-free settings POCO like the other classes in that folder; relocating settings classes wholesale is out of scope).
3. `src/ThePredictions.Infrastructure/DependencyInjection.cs`: no registration change needed; `FieldEncryptionService` now resolves from the `ThePredictions.Infrastructure.Services` namespace already imported at the top of the file (line 23). Confirm the file still compiles without a stray using for the old location.
4. Move the test file to `tests/Unit/ThePredictions.Composition.Tests.Unit/Services/FieldEncryptionServiceTests.cs`, change its namespace to `ThePredictions.Composition.Tests.Unit.Services`, and add `using ThePredictions.Infrastructure.Services;` (keep `using ThePredictions.Application.Configuration;` and `using ThePredictions.Application.Services;` as needed). Add a `// TODO: move to ThePredictions.Infrastructure.Tests.Unit when that project exists (see docs/todo/architecture/test-suite/README.md)` comment at the top.
5. Check the Composition test project has the packages the test file needs (`FluentAssertions`, `xunit.v3` - it does, verified in its `.csproj`).
6. Build and run tests (see Verification checklist).

---

## Work package B: CancellationToken on IEmailService (and the IUserManager decision)

### Current state (verified)

`src/ThePredictions.Application/Services/IEmailService.cs`:

```csharp
public interface IEmailService
{
    Task SendTemplatedEmailAsync(string to, long templateId, object parameters);

    /// <summary>
    /// Sends a templated email and reports the outcome (Brevo message ID on success, error on
    /// failure) instead of swallowing errors. Used by the admin email-test tool.
    /// </summary>
    Task<EmailSendResult> SendTestTemplatedEmailAsync(string to, long templateId, object parameters);
}
```

`src/ThePredictions.Infrastructure/Services/BrevoEmailService.cs` line 26 hardcodes the token:

```csharp
if (!await emailSettingsProvider.AreEmailsEnabledAsync(CancellationToken.None))
```

This is item 4.9 of `docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md` (line 205).

### Target

```csharp
public interface IEmailService
{
    Task SendTemplatedEmailAsync(string to, long templateId, object parameters, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a templated email and reports the outcome (Brevo message ID on success, error on
    /// failure) instead of swallowing errors. Used by the admin email-test tool.
    /// </summary>
    Task<EmailSendResult> SendTestTemplatedEmailAsync(string to, long templateId, object parameters, CancellationToken cancellationToken);
}
```

No default value on the parameter, so every call site is forced to pass a token at compile time.

**Honest limitation:** the `brevo_csharp` SDK methods (`SendTransacEmailAsync`) do not accept a `CancellationToken`, so the token cannot flow into the HTTP call itself. It still buys: cancellation of the DB-backed `AreEmailsEnabledAsync` check, a `cancellationToken.ThrowIfCancellationRequested()` gate immediately before the SDK call (add one in both send paths), and a future-proof interface if the SDK ever gains token overloads.

### Production call sites to update (pass the handler's `cancellationToken`)

Verified by grep for `SendTemplatedEmailAsync|SendTestTemplatedEmailAsync`:

1. `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendPrizeNotificationsCommandHandler.cs` line 82
2. `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendRoundDigestEmailsCommandHandler.cs` line 85
3. `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs` line 73
4. `src/ThePredictions.Application/Features/External/Tasks/Commands/SendLeagueWelcomeEmailsCommandHandler.cs` line 91
5. `src/ThePredictions.Application/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandler.cs` line 50
6. `src/ThePredictions.Application/Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommandHandler.cs` line 52
7. `src/ThePredictions.Application/Features/Authentication/Commands/RequestPasswordReset/RequestPasswordResetCommandHandler.cs` lines 80 and 103 (the private `SendGoogleUserEmailAsync` method must gain a `CancellationToken` parameter; `SendPasswordResetEmailAsync` already has one)
8. `src/ThePredictions.Application/Services/EmailConfirmationSender.cs` line 42
9. `src/ThePredictions.Application/Features/Admin/EmailTests/Commands/SendTestEmailCommandHandler.cs` line 24 (`SendTestTemplatedEmailAsync`)

Implementation: `src/ThePredictions.Infrastructure/Services/BrevoEmailService.cs` - both public methods gain the parameter; line 26 passes it to `AreEmailsEnabledAsync(cancellationToken)`; the private `SendEmailAsync` gains a token parameter and calls `cancellationToken.ThrowIfCancellationRequested()` before `SendTransacEmailAsync`.

### Test files to update

All NSubstitute setups/verifications on `IEmailService` need the extra argument. Per the CLAUDE.md xUnit1051 rule, use `CancellationToken.None` (or `Arg.Any<CancellationToken>()` in `Received`/`DidNotReceive` argument matchers), never a bare `default`:

- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Authentication/Commands/RequestPasswordResetCommandHandlerTests.cs`
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Admin/EmailTests/Commands/SendTestEmailCommandHandlerTests.cs`
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandlerTests.cs`
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/External/Tasks/Commands/SendLeagueWelcomeEmailsCommandHandlerTests.cs`
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Admin/Rounds/Commands/SendRoundDigestEmailsCommandHandlerTests.cs`
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Admin/Rounds/Commands/SendPrizeNotificationsCommandHandlerTests.cs`

Note the existing `DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!)` calls become `...(default!, default, default!, CancellationToken.None)`.

Finally, tick the checkbox on line 205 of `docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md` (`- [ ]` becomes `- [x]`).

### IUserManager: recommendation is NOT to add tokens

`src/ThePredictions.Application/Services/IUserManager.cs` declares 15 token-less async methods, implemented by `src/ThePredictions.Infrastructure/Services/UserManagerService.cs`, which is a thin wrapper over ASP.NET Identity's `UserManager<ApplicationUser>`. Identity's public `UserManager` API takes no `CancellationToken` on any of the wrapped methods (`CreateAsync`, `FindByEmailAsync`, `UpdateAsync`, ...); internally it flows its own protected `CancellationToken` property into the user store (which is how `DapperUserStore` already receives a token). Adding token parameters to `IUserManager` would therefore produce parameters that are accepted and then discarded at the very next line, implying cancellation support that does not exist. The only real gain (a pre-call `ThrowIfCancellationRequested`) does not justify touching 15 methods and roughly 20 call sites. **Decision: leave `IUserManager` token-less.** Revisit only if ASP.NET Identity adds token overloads. Record nothing further; this section is the record.

---

## Work package C: Brevo template resolution behind an abstraction

### Current state (verified)

Eight Application classes inject `IOptions<BrevoSettings>` (`src/ThePredictions.Application/Configuration/BrevoSettings.cs`, with template ids in `src/ThePredictions.Application/Configuration/TemplateSettings.cs`) and pick Brevo template ids themselves. Note: the original finding listed seven; grep found an eighth (`SendScheduledRemindersCommandHandler`).

| # | File | Ctor line | Template property used |
|---|------|-----------|------------------------|
| 1 | `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendPrizeNotificationsCommandHandler.cs` | 19 | `PrizeWon` |
| 2 | `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendRoundDigestEmailsCommandHandler.cs` | 19 | `RoundResultsDigest` |
| 3 | `src/ThePredictions.Application/Features/Admin/Rounds/Commands/SendScheduledRemindersCommandHandler.cs` | 17 | `PredictionsMissing` |
| 4 | `src/ThePredictions.Application/Features/External/Tasks/Commands/SendLeagueWelcomeEmailsCommandHandler.cs` | 17 | `LeagueWelcome` |
| 5 | `src/ThePredictions.Application/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandler.cs` | 9 | `JoinLeagueRequest` |
| 6 | `src/ThePredictions.Application/Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommandHandler.cs` | 9 | `LeagueJoinApproved` |
| 7 | `src/ThePredictions.Application/Features/Authentication/Commands/RequestPasswordReset/RequestPasswordResetCommandHandler.cs` | 17 | `PasswordReset`, `PasswordResetGoogleUser` |
| 8 | `src/ThePredictions.Application/Services/EmailConfirmationSender.cs` | 14 | `EmailConfirmation` |

Each independently re-implements the "template id missing/0" guard. Four inconsistent variants exist today:

**Variant 1 - silent return** (`NotifyMemberOfLeagueApprovalCommandHandler.cs` lines 15-23; `NotifyLeagueAdminOfJoinRequestCommandHandler.cs` has the `Templates == null` half only):

```csharp
if (_brevoSettings.Templates == null)
    return;

var templateId = _brevoSettings.Templates.LeagueJoinApproved;

// 0 = the "you can now take part" template has not been configured in Brevo yet; skip sending
// rather than calling the API with an invalid template id.
if (templateId == 0)
    return;
```

**Variant 2 - LogError and return** (`SendPrizeNotificationsCommandHandler.cs` lines 42-47; same shape in `SendRoundDigestEmailsCommandHandler`, `SendScheduledRemindersCommandHandler`, `SendLeagueWelcomeEmailsCommandHandler`):

```csharp
var templateId = _brevoSettings.Templates?.PrizeWon;
if (!templateId.HasValue || templateId.Value == 0)
{
    logger.LogError("Prize Won: Email template ID not configured.");
    return;
}
```

**Variant 3 - throw** (`RequestPasswordResetCommandHandler.cs` lines 77-78 and 100-101):

```csharp
var templateId = _brevoSettings.Templates?.PasswordReset
    ?? throw new InvalidOperationException("PasswordReset email template ID is not configured");
```

**Variant 4 - LogWarning and return** (`EmailConfirmationSender.cs` lines 30-36).

The existing `src/ThePredictions.Application/Services/IEmailTemplateCatalog.cs` is a different concern: it discovers the live Brevo templates and their merge-tag parameters via the remote API (implementation `src/ThePredictions.Infrastructure/Services/BrevoEmailTemplateCatalog.cs`, cached 5 minutes) to back the admin email-test tool. It does NOT map an application purpose to a configured template id. Leave it unchanged; this package adds a sibling abstraction.

**Policy verification:** `BrevoEmailService.SendEmailAsync` (lines 111-124) already catches `ApiException`, logs an error, and does not rethrow - automated email delivery failure is tolerated, never fatal. The missing-template policy should match: **log an error and skip the send**.

### Target design

New file `src/ThePredictions.Application/Services/EmailTemplatePurpose.cs` (one public type per file):

```csharp
namespace ThePredictions.Application.Services;

/// <summary>
/// The application-level purposes for which a transactional email template can be configured.
/// Members mirror the properties of the template settings so resolution is a direct mapping.
/// </summary>
public enum EmailTemplatePurpose
{
    JoinLeagueRequest,
    LeagueJoinApproved,
    PredictionsMissing,
    PasswordReset,
    PasswordResetGoogleUser,
    EmailConfirmation,
    RoundResultsDigest,
    PrizeWon,
    LeagueWelcome
}
```

New file `src/ThePredictions.Application/Services/IEmailTemplateResolver.cs`:

```csharp
namespace ThePredictions.Application.Services;

/// <summary>
/// Resolves the provider template id configured for an application-level email purpose.
/// Returns null (after logging an error) when no template is configured, so every caller
/// applies the same policy: log once centrally, then skip the send.
/// </summary>
public interface IEmailTemplateResolver
{
    long? Resolve(EmailTemplatePurpose purpose);
}
```

New file `src/ThePredictions.Infrastructure/Services/BrevoEmailTemplateResolver.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

public class BrevoEmailTemplateResolver(
    IOptions<BrevoSettings> settings,
    ILogger<BrevoEmailTemplateResolver> logger) : IEmailTemplateResolver
{
    private readonly BrevoSettings _settings = settings.Value;

    public long? Resolve(EmailTemplatePurpose purpose)
    {
        var templateId = purpose switch
        {
            EmailTemplatePurpose.JoinLeagueRequest => _settings.Templates?.JoinLeagueRequest,
            EmailTemplatePurpose.LeagueJoinApproved => _settings.Templates?.LeagueJoinApproved,
            EmailTemplatePurpose.PredictionsMissing => _settings.Templates?.PredictionsMissing,
            EmailTemplatePurpose.PasswordReset => _settings.Templates?.PasswordReset,
            EmailTemplatePurpose.PasswordResetGoogleUser => _settings.Templates?.PasswordResetGoogleUser,
            EmailTemplatePurpose.EmailConfirmation => _settings.Templates?.EmailConfirmation,
            EmailTemplatePurpose.RoundResultsDigest => _settings.Templates?.RoundResultsDigest,
            EmailTemplatePurpose.PrizeWon => _settings.Templates?.PrizeWon,
            EmailTemplatePurpose.LeagueWelcome => _settings.Templates?.LeagueWelcome,
            _ => null
        };

        if (templateId is null or <= 0)
        {
            logger.LogError("Email template for purpose {EmailTemplatePurpose} is not configured; skipping send.", purpose);
            return null;
        }

        return templateId;
    }
}
```

Register in `src/ThePredictions.Infrastructure/DependencyInjection.cs` next to the other email services (after line 133):

```csharp
services.AddSingleton<IEmailTemplateResolver, BrevoEmailTemplateResolver>();
```

(Singleton is safe: `IOptions<T>` and `ILogger<T>` are singletons; it holds no per-request state. This matches `IEmailTestDefaultsResolver` on line 134.)

Resolution stays as an up-front call in each handler (rather than being folded into `IEmailService`) deliberately: batch handlers must abort BEFORE doing per-recipient work. In particular, `SendPrizeNotificationsCommandHandler` writes `PrizeNotification` sent-log rows and `SendLeagueWelcomeEmailsCommandHandler` writes `LeagueWelcomeNotification` rows after each send; if resolution happened inside the email service, a missing template would silently mark prizes/welcomes as notified without any email. The early return preserves today's abort semantics.

### Call-site changes (all eight)

In each, replace the `IOptions<BrevoSettings> brevoSettings` constructor parameter with `IEmailTemplateResolver templateResolver`, delete the `private readonly BrevoSettings _brevoSettings = brevoSettings.Value;` field, remove the now-unused `using Microsoft.Extensions.Options;` and `using ThePredictions.Application.Configuration;` (where nothing else needs them), and replace the guard with the uniform two-liner:

1. **SendPrizeNotificationsCommandHandler** - replace lines 42-47 with:

```csharp
var templateId = templateResolver.Resolve(EmailTemplatePurpose.PrizeWon);
if (templateId is null)
    return;
```

Then use `templateId.Value` at the send on line 82.

2. **SendRoundDigestEmailsCommandHandler** - same shape, purpose `RoundResultsDigest`, replacing lines 48-53.
3. **SendScheduledRemindersCommandHandler** - same shape, purpose `PredictionsMissing`, replacing lines 52-57 (keep it at the same position in the flow, after the users-to-chase check).
4. **SendLeagueWelcomeEmailsCommandHandler** - purpose `LeagueWelcome`, replacing lines 33-38; keep returning `new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 0, EmailsSent: 0)` on the null path.
5. **NotifyLeagueAdminOfJoinRequestCommandHandler** - delete the `if (_brevoSettings.Templates == null) return;` block (lines 15-16) and the line 38 lookup; resolve `EmailTemplatePurpose.JoinLeagueRequest` at the top of `Handle` and return if null. Behaviour note: a missing template now logs an error instead of returning silently, and the DB read no longer happens when unconfigured (it moves after the resolve) - both improvements, no functional loss.
6. **NotifyMemberOfLeagueApprovalCommandHandler** - delete lines 15-23; resolve `EmailTemplatePurpose.LeagueJoinApproved` at the top and return if null. Same behaviour note as 5.
7. **RequestPasswordResetCommandHandler** - replace both throw-expressions (lines 77-78 and 100-101) with resolve-and-return-if-null inside `SendPasswordResetEmailAsync` / `SendGoogleUserEmailAsync`. Behaviour change (intended): a missing template no longer throws a 500; it logs an error and the endpoint still returns the anti-enumeration success response. In `SendPasswordResetEmailAsync`, resolve BEFORE creating/storing the reset token so an unconfigured system does not accumulate orphan tokens; in the current code the token is stored first - reorder so the resolve guard is the first statement.
8. **EmailConfirmationSender** - replace lines 30-36 with the resolve call. Behaviour note: the log severity for a missing template rises from Warning to Error (the resolver's single policy). Keep the existing code comment explaining that the stored token allows a later resend, and keep the token storage BEFORE the resolve here (unlike item 7, the stored confirmation token is deliberately useful for resends - preserve current ordering).

### Test changes

Update the five test files that build `BrevoSettings`/`TemplateSettings` to instead substitute `IEmailTemplateResolver` (`Substitute.For<IEmailTemplateResolver>()`, with `.Resolve(EmailTemplatePurpose.X).Returns(11L)` or `.Returns((long?)null)` for the not-configured cases):

- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Authentication/Commands/RequestPasswordResetCommandHandlerTests.cs` (line 24 area)
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Admin/Rounds/Commands/SendRoundDigestEmailsCommandHandlerTests.cs` (lines 30, 120)
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Admin/Rounds/Commands/SendPrizeNotificationsCommandHandlerTests.cs` (lines 31, 164)
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/External/Tasks/Commands/SendLeagueWelcomeEmailsCommandHandlerTests.cs` (line 28)
- `tests/Unit/ThePredictions.Application.Tests.Unit/Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandlerTests.cs` (lines 17, 72)

The "not configured" test cases keep their assertions (`DidNotReceive...SendTemplatedEmailAsync`); only the arrangement changes. There are currently no tests for `SendScheduledRemindersCommandHandler`, `NotifyMemberOfLeagueApprovalCommandHandler`, or `EmailConfirmationSender`'s guard - re-check with grep before assuming, but do not add new tests in this plan (test-suite plan scope).

`BrevoSettings` and `TemplateSettings` remain in `src/ThePredictions.Application/Configuration/` for now: after this change they are referenced only by Infrastructure (`BrevoEmailService`, `BrevoEmailTemplateCatalog`, `BrevoEmailTemplateResolver`), `src/ThePredictions.Web/Program.cs` line 65 (`Configure<BrevoSettings>`), and `tests/Unit/ThePredictions.Composition.Tests.Unit/ContainerValidationTests.cs` line 68. Relocating settings POCOs is out of scope (see below).

---

## Work package D: normalise api-sports.io status strings in Infrastructure

### Current state (verified)

Raw provider status codes are interpreted in two Application handlers:

- `src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateScoresForNextRoundCommandHandler.cs` lines 106-118:

```csharp
internal static MatchStatus GetMatchStatus(string apiStatus, bool isKnockout) => apiStatus switch
{
    "FT" or "AET" or "PEN" => MatchStatus.Completed,
    // A knockout tie is scored on the 90-minute result, so once regular time is over the
    // result can no longer change for prediction purposes. Treat the break before extra
    // time (BT), extra time (ET) and the penalty shootout (P) as Completed rather than
    // leaving the match live to the final whistle of the tie - this also lets end-of-round
    // processing fire as soon as the last match is decided on 90 minutes.
    "BT" or "ET" or "P" when isKnockout => MatchStatus.Completed,
    "HT" or "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => MatchStatus.InProgress,
    "PST" => MatchStatus.Postponed,
    _ => MatchStatus.Scheduled
};
```

- `src/ThePredictions.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs`: `"PST"` comparisons at lines 91, 215, 220, 469, 474; the raw string enters via `ValidFixture(..., fixture.Fixture.Status.Short)` at line 79 (private record `ValidFixture` declared at line 540 with `string ApiStatus`).

These are the ONLY raw-status readers in Application (grep for `"FT"|"AET"|"PST"|"PEN"|"BT"|"ET"|"P"` confirmed). The strings come from `src/ThePredictions.Application/FootballApi/DTOs/Status.cs` (`Short` property), reached through `FixtureResponse.Fixture.Status`, returned by `IFootballDataService` (`src/ThePredictions.Application/Services/IFootballDataService.cs`), implemented by `src/ThePredictions.Infrastructure/Services/FootballDataService.cs` (which returns the deserialised `wrapper.Response` untouched in `GetAllFixturesForSeasonAsync` and `GetFixturesByIdsAsync`).

### Coordination note

`docs/todo/architecture/handler-domain-logic-extraction/README.md` (written in parallel) owns moving the knockout 90-minute rule (the `when isKnockout` branch and its comment) into Domain. **This plan owns ONLY the provider-status normalisation**: replacing raw strings with an enum. The knockout special-casing stays exactly where it is, just switching on the enum instead of the string. If the other plan has already landed, apply the same string-to-enum substitution wherever the rule now lives.

### Target design

New file `src/ThePredictions.Application/FootballApi/DTOs/FixtureStatus.cs`:

```csharp
namespace ThePredictions.Application.FootballApi.DTOs;

/// <summary>
/// Provider-neutral fixture status. Infrastructure maps the provider's raw status codes to
/// these values so Application code never interprets provider strings.
/// </summary>
public enum FixtureStatus
{
    /// <summary>Not started, or an unrecognised provider status (safe default).</summary>
    Scheduled,
    /// <summary>In play within regulation time (first half, half time, second half, live).</summary>
    InPlayRegulation,
    /// <summary>Regulation complete but the tie continues (break before extra time, extra time, penalty shootout).</summary>
    InPlayBeyondRegulation,
    /// <summary>Finished at the end of regulation.</summary>
    CompletedAfterRegulation,
    /// <summary>Finished after extra time or a penalty shootout.</summary>
    CompletedAfterExtraTime,
    /// <summary>Postponed.</summary>
    Postponed
}
```

Add to `src/ThePredictions.Application/FootballApi/DTOs/Fixture.cs` (the class currently has `Id`, `TimeZone`, `Date`, `Status`):

```csharp
/// <summary>
/// Normalised status, populated by the Infrastructure data service after deserialisation.
/// Not part of the provider payload.
/// </summary>
[JsonIgnore]
public FixtureStatus NormalisedStatus { get; set; }
```

New file `src/ThePredictions.Infrastructure/Services/ApiSportsStatusMapper.cs` (provider semantics live here):

```csharp
using ThePredictions.Application.FootballApi.DTOs;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Maps api-sports.io short status codes to the provider-neutral <see cref="FixtureStatus"/>.
/// Unrecognised codes map to Scheduled, preserving the previous switch default.
/// </summary>
public static class ApiSportsStatusMapper
{
    public static FixtureStatus Map(string? shortCode) => shortCode switch
    {
        "FT" => FixtureStatus.CompletedAfterRegulation,
        "AET" or "PEN" => FixtureStatus.CompletedAfterExtraTime,
        "BT" or "ET" or "P" => FixtureStatus.InPlayBeyondRegulation,
        "1H" or "HT" or "2H" or "LIVE" => FixtureStatus.InPlayRegulation,
        "PST" => FixtureStatus.Postponed,
        _ => FixtureStatus.Scheduled
    };
}
```

In `src/ThePredictions.Infrastructure/Services/FootballDataService.cs`, in BOTH `GetAllFixturesForSeasonAsync` and `GetFixturesByIdsAsync`, normalise before returning:

```csharp
foreach (var fixtureResponse in wrapper.Response)
{
    if (fixtureResponse.Fixture is not null)
        fixtureResponse.Fixture.NormalisedStatus = ApiSportsStatusMapper.Map(fixtureResponse.Fixture.Status?.Short);
}

return wrapper.Response;
```

### Application call-site changes

1. `UpdateScoresForNextRoundCommandHandler.cs` line 72: pass `fixture.Fixture!.NormalisedStatus` instead of `fixture.Fixture!.Status.Short`. Rewrite `GetMatchStatus` (keeping the knockout comment verbatim):

```csharp
internal static MatchStatus GetMatchStatus(FixtureStatus status, bool isKnockout) => status switch
{
    FixtureStatus.CompletedAfterRegulation or FixtureStatus.CompletedAfterExtraTime => MatchStatus.Completed,
    // (keep the existing knockout comment here)
    FixtureStatus.InPlayBeyondRegulation when isKnockout => MatchStatus.Completed,
    FixtureStatus.InPlayRegulation or FixtureStatus.InPlayBeyondRegulation => MatchStatus.InProgress,
    FixtureStatus.Postponed => MatchStatus.Postponed,
    _ => MatchStatus.Scheduled
};
```

2. `SyncSeasonWithApiCommandHandler.cs`: change the private record at line 540 to `private record ValidFixture(int ExternalId, DateTime MatchDateTimeUtc, int HomeTeamId, int AwayTeamId, string ApiRoundName, FixtureStatus Status);`, pass `fixture.Fixture.NormalisedStatus` at line 79, and replace all five string comparisons: line 91 `f.ApiStatus != "PST"` becomes `f.Status != FixtureStatus.Postponed`; lines 215/469 `fixture.ApiStatus == "PST"` becomes `fixture.Status == FixtureStatus.Postponed`; lines 220/474 analogously.

### Test changes

`tests/Unit/ThePredictions.Application.Tests.Unit/Features/Admin/Rounds/Commands/UpdateScoresForNextRoundCommandHandlerTests.cs` has five `GetMatchStatus` theories using string `InlineData` (lines 14-74). Convert them to `FixtureStatus` `InlineData` values (enum constants are valid attribute arguments). The unrecognised-code theory (`"NS"`, `"TBD"`, `""` map to Scheduled) becomes a test of `ApiSportsStatusMapper.Map` instead; place it at `tests/Unit/ThePredictions.Composition.Tests.Unit/Services/ApiSportsStatusMapperTests.cs` (same temporary-home rationale as work package A - the mapper lives in Infrastructure and only the Composition test project references Infrastructure). Cover every string in the mapper's switch plus the unrecognised/null cases there, and keep an Application-side theory asserting `GetMatchStatus(FixtureStatus.Scheduled, ...)` returns `MatchStatus.Scheduled`.

---

## Behaviour changes (intended, confirm with the user if in doubt)

1. Missing password-reset template: HTTP 500 (throw) becomes log-error-and-skip with the normal success response (work package C, item 7).
2. Missing league join/approval templates: silent skip becomes logged error (C, items 5-6).
3. Missing email-confirmation template: Warning log becomes Error log (C, item 8).
4. Password-reset token is no longer stored when the template is unconfigured (C, item 7 reorder).

No other observable behaviour changes: the status-enum mapping is a mechanical 1:1 translation of the current string switch, and the CancellationToken threading only adds pre-call cancellation gates.

## Out of scope

- Moving `BrevoSettings`, `TemplateSettings`, `FieldEncryptionSettings`, `TimeoutSettings`, `EmailDeliverySettings`, `FootballApiSettings` out of `Application/Configuration` (a wholesale settings-relocation question; they are logic-free POCOs).
- Moving the `FootballApi/DTOs` wire-format classes out of Application (a larger relocation; this plan only stops Application interpreting their raw status strings).
- The knockout 90-minute rule extraction to Domain (`docs/todo/architecture/handler-domain-logic-extraction/README.md` owns it).
- Adding `CancellationToken` to `IUserManager` (decision recorded above: not doing it).
- Creating a `ThePredictions.Infrastructure.Tests.Unit` project and writing new handler tests (test-suite plan scope).
- Query-handler Dapper result-record conversions (already shipped - see the "Dapper Result Mapping" rule in the root `CLAUDE.md`).
- Any database or Brevo template changes (none are needed; template ids and configuration keys are untouched).

## Verification checklist

Run after EACH work package, not just at the end:

- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` succeeds with zero warnings.
- [ ] `dotnet test tests/Unit/ThePredictions.Application.Tests.Unit` passes.
- [ ] `dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit` passes (container validation catches missing DI registrations, e.g. `IEmailTemplateResolver`).
- [ ] `dotnet test tests/Unit/ThePredictions.Domain.Tests.Unit` passes (should be untouched; confirms no accidental Domain edits).
- [ ] Grep confirms no Application file outside `Configuration/` references `BrevoSettings` (`rg "BrevoSettings" src/ThePredictions.Application --type cs` returns only the settings classes themselves).
- [ ] Grep confirms no raw status strings remain in Application: `rg '"PST"|"AET"|"PEN"|"LIVE"' src/ThePredictions.Application --type cs` returns nothing.
- [ ] Grep confirms no `CancellationToken.None` remains inside `BrevoEmailService.SendTemplatedEmailAsync`.
- [ ] `tools\Test Coverage\coverage-unit.bat` still reports 100% line and branch coverage for the Domain project (Domain is untouched, so this should be automatic).
- [ ] Line 205 of `docs/todo/architecture/code-consistency-audit/2026-06-code-review-findings.md` is ticked.
