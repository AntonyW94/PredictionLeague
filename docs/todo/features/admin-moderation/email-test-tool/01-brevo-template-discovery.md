# Task 1: Brevo Template Discovery

**Parent Feature:** [Email Test Tool](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #103) - `BrevoEmailTemplateCatalog` with live discovery and a 5-minute cache.

## Goal

Provide a service that lists all Brevo email templates and extracts the parameter names (`{{ params.X }}` placeholders) from each template's HTML. Results are cached in memory for 5 minutes to stay well under Brevo's API rate limits.

## Where it lives

`ThePredictions.Infrastructure.Services.BrevoTemplateService` implementing
`ThePredictions.Application.Services.IBrevoTemplateService`.

The interface goes in Application; the implementation in Infrastructure (same pattern as `IEmailService` / `BrevoEmailService`).

## Interface

```csharp
namespace ThePredictions.Application.Services;

public interface IBrevoTemplateService
{
    Task<IReadOnlyList<BrevoTemplateSummary>> ListTemplatesAsync(CancellationToken cancellationToken);

    Task<BrevoTemplateDetails> GetTemplateAsync(long templateId, CancellationToken cancellationToken);

    void InvalidateCache();
}

public record BrevoTemplateSummary(long Id, string Name, bool IsActive);

public record BrevoTemplateDetails(
    long Id,
    string Name,
    bool IsActive,
    IReadOnlyList<string> ParameterNames);
```

## Implementation notes

- Use the `brevo_csharp` package already referenced in the Infrastructure project.
- The Brevo SDK exposes `TransactionalEmailsApi.GetSmtpTemplates(...)` for listing and `GetSmtpTemplate(templateId)` for fetching one. Use the `HtmlContent` field on the returned template.
- Extract param names from the HTML with this regex:
  ```csharp
  private static readonly Regex ParamRegex = new(
      @"\{\{\s*params\.(\w+)\s*\}\}",
      RegexOptions.Compiled);
  ```
  Then `Distinct()` and `OrderBy(x => x)` for stable output.
- Cache:
  - One in-memory entry for the full list (TTL 5 min).
  - One entry per `templateId` for details (TTL 5 min).
  - Use `IMemoryCache` (already in the host).
  - `InvalidateCache()` clears both - called by the "Refresh templates" UI button.
- Errors: bubble up as exceptions. The CQRS layer translates them into user-facing messages.

## Configuration

Reuse `BrevoSettings.ApiKey`. No new config required.

## Tests

Unit tests in `ThePredictions.Infrastructure.Tests.Unit`:

- `ExtractParameterNames_ShouldFindAllPlaceholders_WhenMultiplePresent`
- `ExtractParameterNames_ShouldDeduplicate_WhenSameParamUsedTwice`
- `ExtractParameterNames_ShouldHandleConditionals_When{%if%}BlocksUsed`
- `ExtractParameterNames_ShouldReturnEmpty_WhenNoPlaceholders`
- `ExtractParameterNames_ShouldIgnoreSimilarSyntax_When{{user.X}}OrPlainTextBracesUsed`

The Brevo API call itself is wrapped behind a thin internal interface so the cache + parsing logic can be tested without hitting Brevo. A separate (skipped-by-default) integration test can be added later if needed.

## Verification

- [ ] Service registered in `Infrastructure/DependencyInjection.cs` as a singleton (so the cache is shared).
- [ ] Listing templates returns the same data Brevo's dashboard shows.
- [ ] Param extraction matches the placeholders in the existing `Password Reset` and `Password Reset - Google User` templates.
- [ ] Calling within 5 minutes hits the cache (verify with logging or a debugger).
- [ ] `InvalidateCache()` forces a re-fetch on the next call.
