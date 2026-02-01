# Checklist: Creating a New Blazor Component

Use this checklist when adding a new Blazor component to the project.

## Step 1: Determine Component Type

| Type | Location | Use For |
|------|----------|---------|
| Page | `Pages/` | Routable pages with `@page` directive |
| Shared | `Shared/` | Reusable components used across pages |
| Layout | `Layout/` | Layout components |

## Step 2: Create the Component

### Page Component

**Location:** `PredictionLeague.Web.Client/Pages/{Area}/{PageName}.razor`

```razor
@page "/leagues/{LeagueId:int}"
@inject LeagueStateService LeagueState
@inject NavigationManager Navigation
@implements IDisposable

<PageTitle>@LeagueState.League?.Name - Prediction League</PageTitle>

@if (LeagueState.IsLoading)
{
    <LoadingSpinner />
}
else if (LeagueState.League is null)
{
    <NotFound Message="League not found" />
}
else
{
    <div class="league-details">
        <h1 class="text-xl font-bold">@LeagueState.League.Name</h1>
        <p class="text-muted">@LeagueState.League.MemberCount members</p>

        <LeagueStandings LeagueId="LeagueId" />
    </div>
}

@code {
    [Parameter]
    public int LeagueId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        LeagueState.OnStateChange += StateHasChanged;
        await LeagueState.LoadLeagueAsync(LeagueId);
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload if LeagueId changes (navigation between leagues)
        await LeagueState.LoadLeagueAsync(LeagueId);
    }

    public void Dispose()
    {
        LeagueState.OnStateChange -= StateHasChanged;
    }
}
```

### Shared Component

**Location:** `PredictionLeague.Web.Client/Shared/{ComponentName}.razor`

```razor
<div class="league-card @CssClass">
    <div class="league-card__header">
        <h3 class="league-card__title">@League.Name</h3>
        @if (ShowMemberCount)
        {
            <span class="league-card__members text-muted">
                @League.MemberCount members
            </span>
        }
    </div>

    <div class="league-card__body">
        @ChildContent
    </div>

    @if (OnClick.HasDelegate)
    {
        <button class="league-card__action btn btn-primary" @onclick="OnClick">
            @ActionText
        </button>
    }
</div>

@code {
    [Parameter, EditorRequired]
    public LeagueDto League { get; set; } = null!;

    [Parameter]
    public bool ShowMemberCount { get; set; } = true;

    [Parameter]
    public string ActionText { get; set; } = "View";

    [Parameter]
    public string? CssClass { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }
}
```

### Component Checklist

- [ ] Correct location based on component type
- [ ] `@implements IDisposable` if subscribing to events
- [ ] `[Parameter, EditorRequired]` for required parameters
- [ ] `[Parameter]` with defaults for optional parameters
- [ ] Event unsubscription in `Dispose()`
- [ ] UK English spelling in all text
- [ ] No hardcoded colours (use CSS classes)

## Step 3: Create Component CSS (if needed)

**Location:** `PredictionLeague.Web.Client/wwwroot/css/components/{component-name}.css`

```css
.league-card {
    background: var(--card-bg);
    border-radius: var(--radius-lg);
    padding: var(--spacing-4);
}

.league-card__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--spacing-3);
}

.league-card__title {
    font-size: var(--text-lg);
    font-weight: var(--font-semibold);
    color: var(--text-primary);
}

.league-card__members {
    font-size: var(--text-sm);
}

@media (min-width: 768px) {
    .league-card {
        padding: var(--spacing-6);
    }
}
```

### CSS Checklist

- [ ] Uses design tokens (no hardcoded values)
- [ ] Uses BEM naming (`.block__element--modifier`)
- [ ] Mobile-first with `min-width` media queries
- [ ] UK English spelling (`colour` not `color`)
- [ ] Added `@import` to `app.css`
- [ ] Added to `<CssFilesToBundle>` in `.csproj`

See [`new-css-file.md`](new-css-file.md) for full CSS checklist.

## Step 4: Create State Service (if needed)

**Location:** `PredictionLeague.Web.Client/Services/{Area}StateService.cs`

```csharp
public class LeagueStateService
{
    private readonly ILeagueApiClient _apiClient;

    public LeagueStateService(ILeagueApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public LeagueDto? League { get; private set; }
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event Action? OnStateChange;

    public async Task LoadLeagueAsync(int leagueId)
    {
        if (League?.Id == leagueId)
        {
            return; // Already loaded
        }

        IsLoading = true;
        ErrorMessage = null;
        OnStateChange?.Invoke();

        try
        {
            League = await _apiClient.GetLeagueAsync(leagueId);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            League = null;
        }
        finally
        {
            IsLoading = false;
            OnStateChange?.Invoke();
        }
    }

    public void Clear()
    {
        League = null;
        IsLoading = false;
        ErrorMessage = null;
        OnStateChange?.Invoke();
    }
}
```

### State Service Checklist

- [ ] Exposes state via public properties
- [ ] `OnStateChange` event for component updates
- [ ] `IsLoading` property for loading states
- [ ] `ErrorMessage` property for error handling
- [ ] Methods notify via `OnStateChange?.Invoke()`
- [ ] Registered in DI as `AddScoped<>`

## Step 5: Register State Service

**Location:** `PredictionLeague.Web.Client/Program.cs`

```csharp
builder.Services.AddScoped<LeagueStateService>();
```

## Step 6: Create API Client Methods (if needed)

**Location:** `PredictionLeague.Web.Client/Services/ApiClients/I{Area}ApiClient.cs`

```csharp
public interface ILeagueApiClient
{
    Task<LeagueDto?> GetLeagueAsync(int leagueId);
    Task<IEnumerable<LeagueSummaryDto>> GetMyLeaguesAsync();
    Task<LeagueDto> CreateLeagueAsync(CreateLeagueRequest request);
}
```

## Common Patterns

### Loading State

```razor
@if (StateService.IsLoading)
{
    <LoadingSpinner />
}
else if (StateService.ErrorMessage is not null)
{
    <ErrorAlert Message="@StateService.ErrorMessage" />
}
else if (StateService.Data is null)
{
    <EmptyState Message="No data found" />
}
else
{
    <!-- Render content -->
}
```

### Form Handling

```razor
<EditForm Model="@_model" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />

    <div class="form-group">
        <label for="name">League Name</label>
        <InputText id="name" @bind-Value="_model.Name" class="form-control" />
        <ValidationMessage For="() => _model.Name" />
    </div>

    <button type="submit" class="btn btn-primary" disabled="@_isSubmitting">
        @if (_isSubmitting)
        {
            <span>Creating...</span>
        }
        else
        {
            <span>Create League</span>
        }
    </button>
</EditForm>

@code {
    private CreateLeagueModel _model = new();
    private bool _isSubmitting;

    private async Task HandleSubmit()
    {
        _isSubmitting = true;

        try
        {
            await ApiClient.CreateLeagueAsync(_model);
            Navigation.NavigateTo("/leagues");
        }
        catch (ApiException ex)
        {
            // Handle error
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
```

### Navigation

```csharp
// Programmatic navigation
Navigation.NavigateTo("/leagues");
Navigation.NavigateTo($"/leagues/{leagueId}");

// With force reload
Navigation.NavigateTo("/leagues", forceLoad: true);
```

## Verification Checklist

- [ ] Component in correct location (`Pages/`, `Shared/`, `Layout/`)
- [ ] `@implements IDisposable` if using event subscriptions
- [ ] Event subscriptions cleaned up in `Dispose()`
- [ ] Parameters documented with `[Parameter]` attribute
- [ ] Required parameters use `[EditorRequired]`
- [ ] Loading states handled
- [ ] Error states handled
- [ ] Empty states handled
- [ ] CSS uses design tokens (no hardcoded values)
- [ ] CSS added to both `app.css` and `.csproj` bundle
- [ ] State service registered in DI (if created)
- [ ] UK English spelling throughout
- [ ] No hardcoded colours in Razor or CSS
