# Task 7: Dashboard Component

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a Blazor component to display league insights on the league dashboard, showing contention status, probabilities, and eliminated users.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web.Client/Features/Leagues/Components/LeagueInsights.razor` | Create | Main insights display component |
| `PredictionLeague.Web.Client/Features/Leagues/Components/LeagueInsights.razor.cs` | Create | Component code-behind |
| `PredictionLeague.Web.Client/Features/Leagues/Pages/LeagueDashboard.razor` | Modify | Include insights component |
| `PredictionLeague.Web.Client/wwwroot/css/components/insights.css` | Create | Styling for insights |
| `PredictionLeague.Web.Client/wwwroot/css/app.css` | Modify | Import new CSS |

## Implementation Steps

### Step 1: Create LeagueInsights Component

```razor
@* PredictionLeague.Web.Client/Features/Leagues/Components/LeagueInsights.razor *@

@if (IsLoading)
{
    <div class="insights-loading">
        <span class="spinner"></span>
        <span>Calculating scenarios...</span>
    </div>
}
else if (Insights is not null)
{
    <div class="insights-panel">
        <div class="insights-header">
            <h3>📊 Round @Insights.RoundNumber Insights</h3>
            <span class="insights-matches">
                @Insights.CompletedMatches/@Insights.TotalMatches matches played
                @if (Insights.LiveMatches > 0)
                {
                    <span class="live-indicator">● @Insights.LiveMatches live</span>
                }
            </span>
        </div>

        @if (Insights.IsLastRoundOfMonth)
        {
            <div class="insights-month-badge">
                Final round of @GetMonthName(Insights.Month)
            </div>
        }

        <div class="insights-content @(Insights.IsLastRoundOfMonth ? "has-monthly" : "")">
            @* Round Contention *@
            <div class="insights-section">
                <h4>Round Contention</h4>
                <div class="contention-table">
                    @foreach (var contender in Insights.Contenders)
                    {
                        <div class="contender-row @(contender.UserId == CurrentUserId ? "is-current-user" : "")"
                             @onclick="() => OnContenderClick(contender)">
                            <span class="contender-name">
                                @if (contender.UserId == CurrentUserId)
                                {
                                    <strong>You</strong>
                                }
                                else
                                {
                                    @contender.UserName
                                }
                            </span>
                            <span class="contender-points">@contender.CurrentRoundPoints pts</span>
                            <span class="contender-probability win">@contender.RoundWinProbability%</span>
                            @if (contender.RoundTieProbability > 0)
                            {
                                <span class="contender-probability tie">+@contender.RoundTieProbability% tie</span>
                            }
                            @if (contender.HasBoostApplied)
                            {
                                <span class="boost-indicator" title="Boost applied">⚡</span>
                            }
                        </div>
                    }

                    @if (Insights.EliminatedUsers.Any())
                    {
                        <div class="eliminated-section">
                            <button class="eliminated-toggle" @onclick="ToggleEliminated">
                                @(ShowEliminated ? "Hide" : "Show") eliminated (@Insights.EliminatedUsers.Count)
                            </button>
                            @if (ShowEliminated)
                            {
                                @foreach (var user in Insights.EliminatedUsers)
                                {
                                    <div class="contender-row eliminated">
                                        <span class="contender-name">
                                            @if (user.UserId == CurrentUserId)
                                            {
                                                <strong>You</strong>
                                            }
                                            else
                                            {
                                                @user.UserName
                                            }
                                        </span>
                                        <span class="contender-points">@user.CurrentRoundPoints pts</span>
                                        <span class="eliminated-label">Eliminated</span>
                                    </div>
                                }
                            }
                        </div>
                    }
                </div>
            </div>

            @* Monthly Contention (only for last round of month) *@
            @if (Insights.IsLastRoundOfMonth)
            {
                <div class="insights-section monthly">
                    <h4>@GetMonthName(Insights.Month) Contention</h4>
                    <div class="contention-table">
                        @foreach (var contender in GetMonthlyContenders())
                        {
                            <div class="contender-row @(contender.UserId == CurrentUserId ? "is-current-user" : "")"
                                 @onclick="() => OnContenderClick(contender)">
                                <span class="contender-name">
                                    @if (contender.UserId == CurrentUserId)
                                    {
                                        <strong>You</strong>
                                    }
                                    else
                                    {
                                        @contender.UserName
                                    }
                                </span>
                                <span class="contender-points">@contender.CurrentMonthlyPoints pts</span>
                                <span class="contender-probability win">@contender.MonthlyWinProbability%</span>
                                @if (contender.MonthlyTieProbability > 0)
                                {
                                    <span class="contender-probability tie">+@contender.MonthlyTieProbability% tie</span>
                                }
                            </div>
                        }
                    </div>

                    @* Combined probability for current user *@
                    @{
                        var currentUserInsights = Insights.Contenders.FirstOrDefault(c => c.UserId == CurrentUserId);
                        if (currentUserInsights?.BothWinProbability > 0)
                        {
                            <div class="combined-probability">
                                <span>@currentUserInsights.BothWinProbability% chance to win both round AND month</span>
                            </div>
                        }
                    }
                </div>
            }
        </div>

        <div class="insights-footer">
            <button class="btn-view-scenarios" @onclick="OnViewMyScenarios">
                View your winning scenarios
            </button>
            <span class="insights-timestamp">
                Updated @Insights.GeneratedAtUtc.ToLocalTime().ToString("HH:mm")
            </span>
        </div>
    </div>
}
else if (HasError)
{
    <div class="insights-error">
        Unable to load insights. <button @onclick="LoadInsights">Retry</button>
    </div>
}
@* No insights panel shown when Insights is null and no error (no in-progress round) *@
```

### Step 2: Create Component Code-Behind

```csharp
// PredictionLeague.Web.Client/Features/Leagues/Components/LeagueInsights.razor.cs
using Microsoft.AspNetCore.Components;
using PredictionLeague.Contracts.Leagues.Insights;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Features.Leagues.Components;

public partial class LeagueInsights : ComponentBase
{
    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public int LeagueId { get; set; }

    [Parameter]
    public string? CurrentUserId { get; set; }

    [Parameter]
    public EventCallback<ContenderInsights> OnContenderSelected { get; set; }

    private LeagueInsightsSummary? Insights { get; set; }
    private bool IsLoading { get; set; }
    private bool HasError { get; set; }
    private bool ShowEliminated { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadInsights();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload if league changes
        if (Insights?.LeagueId != LeagueId)
        {
            await LoadInsights();
        }
    }

    private async Task LoadInsights()
    {
        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            Insights = await Http.GetFromJsonAsync<LeagueInsightsSummary>(
                $"api/leagues/{LeagueId}/insights");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // No in-progress round - this is expected, not an error
            Insights = null;
        }
        catch (Exception)
        {
            HasError = true;
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void ToggleEliminated()
    {
        ShowEliminated = !ShowEliminated;
    }

    private async Task OnContenderClick(ContenderInsights contender)
    {
        if (OnContenderSelected.HasDelegate)
        {
            await OnContenderSelected.InvokeCallback(contender);
        }
    }

    private void OnViewMyScenarios()
    {
        if (!string.IsNullOrEmpty(CurrentUserId))
        {
            OnContenderClick(Insights!.Contenders.First(c => c.UserId == CurrentUserId));
        }
    }

    private IEnumerable<ContenderInsights> GetMonthlyContenders()
    {
        // Order by monthly probability for the monthly section
        return Insights!.Contenders
            .Where(c => c.MonthlyWinProbability.HasValue)
            .OrderByDescending(c => c.MonthlyWinProbability)
            .ThenByDescending(c => c.MonthlyTieProbability);
    }

    private static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM");
    }
}
```

### Step 3: Create CSS Styles

```css
/* PredictionLeague.Web.Client/wwwroot/css/components/insights.css */

/* ============================================
   League Insights Panel
   ============================================ */

.insights-panel {
    background: var(--purple-900);
    border-radius: var(--radius-lg);
    padding: 1rem;
    margin-bottom: 1rem;
}

.insights-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 0.75rem;
}

.insights-header h3 {
    margin: 0;
    font-size: 1rem;
    color: var(--white);
}

.insights-matches {
    font-size: 0.875rem;
    color: var(--purple-300);
}

.live-indicator {
    color: var(--green-500);
    margin-left: 0.5rem;
    animation: pulse 2s infinite;
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.5; }
}

.insights-month-badge {
    display: inline-block;
    background: var(--gold);
    color: var(--purple-1000);
    padding: 0.25rem 0.75rem;
    border-radius: 1rem;
    font-size: 0.75rem;
    font-weight: 600;
    margin-bottom: 0.75rem;
}

/* Content layout */
.insights-content {
    display: grid;
    gap: 1rem;
}

.insights-content.has-monthly {
    grid-template-columns: 1fr 1fr;
}

@media (max-width: 768px) {
    .insights-content.has-monthly {
        grid-template-columns: 1fr;
    }
}

/* Section styling */
.insights-section {
    background: var(--purple-800);
    border-radius: var(--radius-md);
    padding: 0.75rem;
}

.insights-section h4 {
    margin: 0 0 0.5rem 0;
    font-size: 0.875rem;
    color: var(--purple-200);
}

.insights-section.monthly h4 {
    color: var(--gold);
}

/* Contention table */
.contention-table {
    display: flex;
    flex-direction: column;
    gap: 0.375rem;
}

.contender-row {
    display: grid;
    grid-template-columns: 1fr auto auto auto auto;
    gap: 0.5rem;
    align-items: center;
    padding: 0.375rem 0.5rem;
    background: var(--purple-700);
    border-radius: var(--radius-sm);
    cursor: pointer;
    transition: background 0.2s ease;
}

.contender-row:hover {
    background: var(--purple-600);
}

.contender-row.is-current-user {
    background: var(--purple-600);
    border-left: 3px solid var(--gold);
}

.contender-row.eliminated {
    opacity: 0.6;
    cursor: default;
}

.contender-row.eliminated:hover {
    background: var(--purple-700);
}

.contender-name {
    font-size: 0.875rem;
    color: var(--white);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.contender-points {
    font-size: 0.75rem;
    color: var(--purple-300);
}

.contender-probability {
    font-size: 0.75rem;
    font-weight: 600;
    padding: 0.125rem 0.375rem;
    border-radius: 0.25rem;
}

.contender-probability.win {
    background: var(--green-600);
    color: var(--white);
}

.contender-probability.tie {
    background: var(--blue-500);
    color: var(--purple-1000);
}

.boost-indicator {
    font-size: 0.875rem;
}

.eliminated-label {
    font-size: 0.75rem;
    color: var(--red-400);
}

/* Eliminated section */
.eliminated-section {
    margin-top: 0.5rem;
    padding-top: 0.5rem;
    border-top: 1px solid var(--purple-600);
}

.eliminated-toggle {
    background: none;
    border: none;
    color: var(--purple-300);
    font-size: 0.75rem;
    cursor: pointer;
    padding: 0.25rem 0;
    margin-bottom: 0.375rem;
}

.eliminated-toggle:hover {
    color: var(--purple-200);
}

/* Combined probability */
.combined-probability {
    margin-top: 0.75rem;
    padding: 0.5rem;
    background: var(--purple-700);
    border-radius: var(--radius-sm);
    text-align: center;
    font-size: 0.875rem;
    color: var(--gold);
}

/* Footer */
.insights-footer {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-top: 0.75rem;
    padding-top: 0.75rem;
    border-top: 1px solid var(--purple-700);
}

.btn-view-scenarios {
    background: var(--purple-600);
    border: none;
    color: var(--white);
    padding: 0.5rem 1rem;
    border-radius: var(--radius-sm);
    font-size: 0.875rem;
    cursor: pointer;
    transition: background 0.2s ease;
}

.btn-view-scenarios:hover {
    background: var(--purple-500);
}

.insights-timestamp {
    font-size: 0.75rem;
    color: var(--purple-400);
}

/* Loading state */
.insights-loading {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 1rem;
    color: var(--purple-300);
}

.insights-loading .spinner {
    width: 1rem;
    height: 1rem;
    border: 2px solid var(--purple-600);
    border-top-color: var(--purple-300);
    border-radius: 50%;
    animation: spin 1s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}

/* Error state */
.insights-error {
    padding: 1rem;
    background: var(--red-900);
    border-radius: var(--radius-md);
    color: var(--red-200);
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

.insights-error button {
    background: var(--red-700);
    border: none;
    color: var(--white);
    padding: 0.25rem 0.5rem;
    border-radius: var(--radius-sm);
    cursor: pointer;
}
```

### Step 4: Update app.css to Import New Styles

```css
/* In app.css, add import */
@import 'components/insights.css';
```

### Step 5: Add Component to League Dashboard

```razor
@* In LeagueDashboard.razor, add where appropriate *@

<LeagueInsights
    LeagueId="@LeagueId"
    CurrentUserId="@CurrentUserId"
    OnContenderSelected="HandleContenderSelected" />

@code {
    private async Task HandleContenderSelected(ContenderInsights contender)
    {
        // Open scenarios modal
        SelectedContender = contender;
        ShowScenariosModal = true;
    }
}
```

## Code Patterns to Follow

Follow existing Blazor component patterns:

```razor
@* Example from existing components *@
@if (IsLoading)
{
    <LoadingSpinner />
}
else if (Data is not null)
{
    <div class="component">
        @* Content *@
    </div>
}
```

## Verification

- [ ] Component loads and displays insights when round is in progress
- [ ] Component hides gracefully when no in-progress round (no error)
- [ ] Current user is highlighted in the list
- [ ] Clicking a contender triggers the callback
- [ ] Eliminated users toggle works correctly
- [ ] Monthly section only shows for last round of month
- [ ] Responsive layout works on mobile
- [ ] Loading and error states display correctly

## Edge Cases to Consider

- No in-progress round (component should not render)
- Current user is eliminated (show them in eliminated section, highlighted)
- All users are eliminated except one (100% probability)
- Very long usernames (truncate with ellipsis)
- Many contenders (may need scrolling)
- Probabilities that round to 0% but user isn't eliminated

## Notes

- The component doesn't poll for updates - user must refresh or navigate away/back
- Consider adding auto-refresh during live matches in future enhancement
- CSS uses existing colour variables from the design system
- Boost indicator shows lightning bolt emoji for users with active boosts
