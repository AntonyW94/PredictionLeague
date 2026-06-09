# Task 4: Blazor Page

**Parent Feature:** [Email Test Tool](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #103) - the `/admin/email-tests` Blazor page with template dropdown, parameter form, and send result.

## Goal

The admin-only Blazor page that ties the API together. Two dropdowns, a dynamic param form, a send button, and a status panel.

## Route and authorisation

- Route: `/admin/email-tests`
- Wrap the page in an `<AuthorizeView Roles="@RoleNames.Administrator">` (matching how other admin pages are gated). Show a 403-style "Not authorised" panel for non-admins (or rely on the router-level authorisation if that's the existing pattern - check `/admin/users`).

## State management

Per the project's [Blazor guidelines](../../../../src/ThePredictions.Web.Client/CLAUDE.md), state lives in a service and the component subscribes to `OnStateChange`.

Create `Services/Admin/EmailTestsStateService.cs` with:

```csharp
public class EmailTestsStateService
{
    public IReadOnlyList<EmailTemplateSummaryDto> Templates { get; private set; } = [];
    public IReadOnlyList<UserDto> Users { get; private set; } = [];

    public EmailTemplateDetailsDto? SelectedTemplate { get; private set; }
    public Guid? SelectedUserId { get; private set; }
    public Dictionary<string, string> ParamValues { get; private set; } = new();

    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public string? LastMessageId { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastSentAtUtc { get; private set; }

    public event Action? OnStateChange;

    public Task InitialiseAsync();           // load templates + users
    public Task SelectTemplateAsync(long id);
    public Task SelectUserAsync(Guid id);
    public Task SendAsync();
    public Task RefreshTemplatesAsync();
}
```

`SelectTemplateAsync` and `SelectUserAsync` both refresh the param defaults by calling `GET /templates/{id}?dataSourceUserId=...` and replacing `ParamValues`. Existing user edits to `ParamValues` are lost on switch - acceptable, and matches user expectation.

## Page layout

```razor
@page "/admin/email-tests"
@attribute [Authorize(Roles = RoleNames.Administrator)]
@inject EmailTestsStateService State
@implements IDisposable

<PageTitle>Email Test Tool</PageTitle>

<div class="email-test-page">
    <header class="email-test-page__header">
        <h1>Email Test Tool</h1>
        <button class="btn btn-secondary" @onclick="State.RefreshTemplatesAsync">
            <span class="bi bi-arrow-clockwise me-2"></span>Refresh
        </button>
    </header>

    @if (State.IsLoading)
    {
        <LoadingSpinner />
    }
    else
    {
        <div class="email-test-page__pickers">
            <label>
                Template
                <select @onchange="OnTemplateChanged">
                    <option value="">Select a template...</option>
                    @foreach (var t in State.Templates)
                    {
                        <option value="@t.Id" disabled="@(!t.IsActive)">
                            @t.Name @(t.IsActive ? "" : "(inactive)")
                        </option>
                    }
                </select>
            </label>

            <label>
                Pre-fill from user
                <select @onchange="OnUserChanged">
                    @foreach (var u in State.Users)
                    {
                        <option value="@u.Id">@u.FirstName @u.LastName (@u.Email)</option>
                    }
                </select>
            </label>
        </div>

        @if (State.SelectedTemplate is { IsActive: false })
        {
            <div class="alert alert-warning">This template is inactive in Brevo and cannot be sent.</div>
        }
        else if (State.SelectedTemplate is { } template)
        {
            <div class="email-test-page__params">
                <h2>Parameters</h2>
                @foreach (var param in template.Parameters)
                {
                    <label>
                        @param.Name
                        <input type="text"
                               value="@State.ParamValues[param.Name]"
                               @onchange="@(e => OnParamChanged(param.Name, e.Value?.ToString() ?? ""))" />
                    </label>
                }
            </div>

            <p class="email-test-page__send-target">
                Sends to: <strong>@CallingAdminEmail</strong> (your account).
            </p>

            <button class="btn btn-primary"
                    disabled="@State.IsSending"
                    @onclick="State.SendAsync">
                @(State.IsSending ? "Sending..." : "Send test email")
            </button>
        }

        @if (State.LastMessageId is not null)
        {
            <div class="alert alert-success">
                Sent @LastSentAgo - Brevo message ID: <code>@State.LastMessageId</code>
            </div>
        }
        @if (State.LastError is not null)
        {
            <div class="alert alert-danger">@State.LastError</div>
        }
    }
</div>

@code {
    private string CallingAdminEmail = "";  // pulled from claims on init

    protected override async Task OnInitializedAsync()
    {
        State.OnStateChange += StateHasChanged;
        await State.InitialiseAsync();
    }

    public void Dispose() => State.OnStateChange -= StateHasChanged;
}
```

(The `CallingAdminEmail` claim lookup is the same as how the user avatar reads `FirstName` in `NavLayout.razor`.)

## Styling

New CSS file: `wwwroot/css/pages/email-test-page.css`. Per
[`docs/guides/checklists/new-css-file.md`](../../../guides/checklists/new-css-file.md):

1. Add `@import` to `wwwroot/css/app.css`.
2. Add the file to `<CssFilesToBundle>` in `src/ThePredictions.Web/ThePredictions.Web.csproj`.

Use design tokens only. Verify in light *and* dark mode.

## Verification

- [ ] Navigating to `/admin/email-tests` as a non-admin redirects or shows 403.
- [ ] Page loads, dropdowns populate.
- [ ] Selecting a template renders the right number of inputs.
- [ ] Defaults are realistic (firstName matches the picked user, resetLink looks like a real URL).
- [ ] Clicking Send delivers a real email to the admin's inbox.
- [ ] The reset link in the test email opens the reset password page and works.
- [ ] Inactive template dropdown items are greyed out and unselectable.
- [ ] Refresh button repopulates the template list (if you've just added one in Brevo).
- [ ] Page renders cleanly in both light and dark mode.
