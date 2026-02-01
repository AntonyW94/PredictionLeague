# Task 8: Scenarios Modal

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a modal component to display detailed winning scenarios for a selected contender, including match constraints and the ability to view all winning scenario combinations.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web.Client/Features/Leagues/Components/ScenariosModal.razor` | Create | Modal for detailed scenarios |
| `PredictionLeague.Web.Client/Features/Leagues/Components/ScenariosModal.razor.cs` | Create | Component code-behind |
| `PredictionLeague.Web.Client/wwwroot/css/components/scenarios-modal.css` | Create | Modal styling |
| `PredictionLeague.Web.Client/wwwroot/css/app.css` | Modify | Import new CSS |

## Implementation Steps

### Step 1: Create ScenariosModal Component

```razor
@* PredictionLeague.Web.Client/Features/Leagues/Components/ScenariosModal.razor *@

@if (IsVisible)
{
    <div class="modal-overlay" @onclick="Close">
        <div class="scenarios-modal" @onclick:stopPropagation>
            <div class="modal-header">
                <h2>
                    @if (Contender?.UserId == CurrentUserId)
                    {
                        <span>Your Winning Scenarios</span>
                    }
                    else
                    {
                        <span>@Contender?.UserName's Winning Scenarios</span>
                    }
                </h2>
                <button class="modal-close" @onclick="Close">×</button>
            </div>

            @if (IsLoading)
            {
                <div class="modal-loading">
                    <span class="spinner"></span>
                    <span>Loading detailed scenarios...</span>
                </div>
            }
            else if (DetailedContender is not null)
            {
                <div class="modal-content">
                    @* Summary stats *@
                    <div class="scenarios-summary">
                        <div class="summary-stat">
                            <span class="stat-value">@DetailedContender.RoundWinScenarioCount</span>
                            <span class="stat-label">Round wins</span>
                            <span class="stat-percent">(@DetailedContender.RoundWinProbability%)</span>
                        </div>
                        @if (DetailedContender.RoundTieScenarioCount > 0)
                        {
                            <div class="summary-stat">
                                <span class="stat-value">@DetailedContender.RoundTieScenarioCount</span>
                                <span class="stat-label">Round ties</span>
                                <span class="stat-percent">(@DetailedContender.RoundTieProbability%)</span>
                            </div>
                        }
                        @if (DetailedContender.MonthlyWinScenarioCount.HasValue)
                        {
                            <div class="summary-stat monthly">
                                <span class="stat-value">@DetailedContender.MonthlyWinScenarioCount</span>
                                <span class="stat-label">Month wins</span>
                                <span class="stat-percent">(@DetailedContender.MonthlyWinProbability%)</span>
                            </div>
                        }
                    </div>

                    @* Tab selection *@
                    <div class="scenarios-tabs">
                        <button class="tab @(ActiveTab == "constraints" ? "active" : "")"
                                @onclick="() => ActiveTab = "constraints"">
                            What You Need
                        </button>
                        <button class="tab @(ActiveTab == "scenarios" ? "active" : "")"
                                @onclick="() => ActiveTab = "scenarios"">
                            All Scenarios (@(DetailedContender.WinningScenarios?.Count ?? 0))
                        </button>
                    </div>

                    @* Tab content *@
                    @if (ActiveTab == "constraints")
                    {
                        <div class="constraints-view">
                            @foreach (var constraint in DetailedContender.MatchConstraints)
                            {
                                <div class="constraint-card @(constraint.AnyResultWorks ? "any-result" : "")">
                                    <div class="constraint-match">
                                        <span class="home-team">@constraint.HomeTeam</span>
                                        <span class="vs">vs</span>
                                        <span class="away-team">@constraint.AwayTeam</span>
                                    </div>
                                    <div class="constraint-requirement">
                                        @if (constraint.AnyResultWorks && constraint.ExcludedScorelines.Count == 0)
                                        {
                                            <span class="any-result-badge">✓ Any result works</span>
                                        }
                                        else
                                        {
                                            <span class="need-label">Need:</span>
                                            <span class="need-value">@constraint.GetDescription()</span>
                                        }
                                    </div>
                                    @if (constraint.ExcludedScorelines.Any())
                                    {
                                        <div class="excluded-scores">
                                            <span class="excluded-label">Except:</span>
                                            @foreach (var score in constraint.ExcludedScorelines)
                                            {
                                                <span class="excluded-score" title="@score.Reason">
                                                    @score.HomeScore-@score.AwayScore
                                                    @if (!string.IsNullOrEmpty(score.Reason))
                                                    {
                                                        <span class="reason">(@score.Reason)</span>
                                                    }
                                                </span>
                                            }
                                        </div>
                                    }
                                </div>
                            }
                        </div>
                    }
                    else if (ActiveTab == "scenarios")
                    {
                        <div class="scenarios-list-view">
                            @if (DetailedContender.WinningScenarios is null || !DetailedContender.WinningScenarios.Any())
                            {
                                <p class="no-scenarios">No winning scenarios available.</p>
                            }
                            else
                            {
                                <div class="scenarios-filter">
                                    <label>
                                        <input type="checkbox" @bind="FilterRoundWins" />
                                        Round wins only
                                    </label>
                                    @if (DetailedContender.MonthlyWinScenarioCount.HasValue)
                                    {
                                        <label>
                                            <input type="checkbox" @bind="FilterMonthlyWins" />
                                            Month wins only
                                        </label>
                                        <label>
                                            <input type="checkbox" @bind="FilterBothWins" />
                                            Both wins only
                                        </label>
                                    }
                                </div>

                                <div class="scenarios-list">
                                    @foreach (var scenario in GetFilteredScenarios().Take(MaxDisplayedScenarios))
                                    {
                                        <div class="scenario-card">
                                            <div class="scenario-header">
                                                <span class="scenario-id">#@scenario.ScenarioId</span>
                                                @if (scenario.WinsRoundOutright)
                                                {
                                                    <span class="scenario-badge win">Round Win</span>
                                                }
                                                else if (scenario.TiesForRound)
                                                {
                                                    <span class="scenario-badge tie">Round Tie</span>
                                                }
                                                @if (scenario.WinsMonthOutright)
                                                {
                                                    <span class="scenario-badge win monthly">Month Win</span>
                                                }
                                                else if (scenario.TiesForMonth)
                                                {
                                                    <span class="scenario-badge tie monthly">Month Tie</span>
                                                }
                                            </div>
                                            <div class="scenario-results">
                                                @foreach (var result in scenario.MatchResults)
                                                {
                                                    <div class="scenario-result @(result.IsGenericResult ? "generic" : "")">
                                                        <span class="team-abbrev">@GetAbbreviation(result.HomeTeam)</span>
                                                        <span class="score">
                                                            @if (result.IsGenericResult)
                                                            {
                                                                @GetGenericResultDisplay(result)
                                                            }
                                                            else
                                                            {
                                                                @result.HomeScore-@result.AwayScore
                                                            }
                                                        </span>
                                                        <span class="team-abbrev">@GetAbbreviation(result.AwayTeam)</span>
                                                    </div>
                                                }
                                            </div>
                                            <div class="scenario-points">
                                                @scenario.FinalRoundPoints pts
                                                @if (scenario.FinalMonthlyPoints != scenario.FinalRoundPoints)
                                                {
                                                    <span class="monthly-points">(@scenario.FinalMonthlyPoints monthly)</span>
                                                }
                                            </div>
                                        </div>
                                    }

                                    @{
                                        var totalFiltered = GetFilteredScenarios().Count();
                                        if (totalFiltered > MaxDisplayedScenarios)
                                        {
                                            <div class="scenarios-more">
                                                Showing @MaxDisplayedScenarios of @totalFiltered scenarios
                                                <button @onclick="ShowMoreScenarios">Show more</button>
                                            </div>
                                        }
                                    }
                                </div>
                            }
                        </div>
                    }
                </div>
            }
            else if (HasError)
            {
                <div class="modal-error">
                    <p>Unable to load scenarios.</p>
                    <button @onclick="LoadDetailedScenarios">Retry</button>
                </div>
            }
        </div>
    </div>
}
```

### Step 2: Create Component Code-Behind

```csharp
// PredictionLeague.Web.Client/Features/Leagues/Components/ScenariosModal.razor.cs
using Microsoft.AspNetCore.Components;
using PredictionLeague.Contracts.Leagues.Insights;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Features.Leagues.Components;

public partial class ScenariosModal : ComponentBase
{
    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Parameter]
    public bool IsVisible { get; set; }

    [Parameter]
    public EventCallback<bool> IsVisibleChanged { get; set; }

    [Parameter]
    public int LeagueId { get; set; }

    [Parameter]
    public ContenderInsights? Contender { get; set; }

    [Parameter]
    public string? CurrentUserId { get; set; }

    private ContenderInsights? DetailedContender { get; set; }
    private bool IsLoading { get; set; }
    private bool HasError { get; set; }
    private string ActiveTab { get; set; } = "constraints";

    // Scenario filters
    private bool FilterRoundWins { get; set; }
    private bool FilterMonthlyWins { get; set; }
    private bool FilterBothWins { get; set; }
    private int MaxDisplayedScenarios { get; set; } = 50;

    protected override async Task OnParametersSetAsync()
    {
        if (IsVisible && Contender is not null && DetailedContender?.UserId != Contender.UserId)
        {
            await LoadDetailedScenarios();
        }
    }

    private async Task LoadDetailedScenarios()
    {
        if (Contender is null) return;

        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            DetailedContender = await Http.GetFromJsonAsync<ContenderInsights>(
                $"api/leagues/{LeagueId}/insights/users/{Contender.UserId}/scenarios");
        }
        catch
        {
            HasError = true;
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task Close()
    {
        await IsVisibleChanged.InvokeAsync(false);
        // Reset state
        DetailedContender = null;
        ActiveTab = "constraints";
        FilterRoundWins = false;
        FilterMonthlyWins = false;
        FilterBothWins = false;
        MaxDisplayedScenarios = 50;
    }

    private IEnumerable<WinningScenario> GetFilteredScenarios()
    {
        if (DetailedContender?.WinningScenarios is null)
            return Enumerable.Empty<WinningScenario>();

        var scenarios = DetailedContender.WinningScenarios.AsEnumerable();

        if (FilterRoundWins)
            scenarios = scenarios.Where(s => s.WinsRoundOutright);

        if (FilterMonthlyWins)
            scenarios = scenarios.Where(s => s.WinsMonthOutright);

        if (FilterBothWins)
            scenarios = scenarios.Where(s => s.WinsRoundOutright && s.WinsMonthOutright);

        return scenarios;
    }

    private void ShowMoreScenarios()
    {
        MaxDisplayedScenarios += 50;
    }

    private static string GetAbbreviation(string teamName)
    {
        // Simple abbreviation - first 3 letters
        return teamName.Length > 3 ? teamName[..3].ToUpper() : teamName.ToUpper();
    }

    private static string GetGenericResultDisplay(ScenarioMatchResult result)
    {
        return result.ResultType switch
        {
            ResultType.HomeWin => "H",
            ResultType.Draw => "D",
            ResultType.AwayWin => "A",
            _ => "?"
        };
    }
}
```

### Step 3: Create Modal CSS Styles

```css
/* PredictionLeague.Web.Client/wwwroot/css/components/scenarios-modal.css */

/* ============================================
   Scenarios Modal
   ============================================ */

.modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.7);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 1000;
    padding: 1rem;
}

.scenarios-modal {
    background: var(--purple-900);
    border-radius: var(--radius-lg);
    width: 100%;
    max-width: 600px;
    max-height: 90vh;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1rem 1.25rem;
    border-bottom: 1px solid var(--purple-700);
}

.modal-header h2 {
    margin: 0;
    font-size: 1.125rem;
    color: var(--white);
}

.modal-close {
    background: none;
    border: none;
    color: var(--purple-300);
    font-size: 1.5rem;
    cursor: pointer;
    padding: 0.25rem;
    line-height: 1;
}

.modal-close:hover {
    color: var(--white);
}

.modal-content {
    flex: 1;
    overflow-y: auto;
    padding: 1rem 1.25rem;
}

/* Summary stats */
.scenarios-summary {
    display: flex;
    gap: 1rem;
    margin-bottom: 1rem;
}

.summary-stat {
    flex: 1;
    background: var(--purple-800);
    border-radius: var(--radius-md);
    padding: 0.75rem;
    text-align: center;
}

.summary-stat.monthly {
    border: 1px solid var(--gold);
}

.stat-value {
    display: block;
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--white);
}

.stat-label {
    display: block;
    font-size: 0.75rem;
    color: var(--purple-300);
}

.stat-percent {
    display: block;
    font-size: 0.875rem;
    color: var(--green-500);
}

/* Tabs */
.scenarios-tabs {
    display: flex;
    gap: 0.5rem;
    margin-bottom: 1rem;
}

.scenarios-tabs .tab {
    flex: 1;
    background: var(--purple-800);
    border: none;
    color: var(--purple-300);
    padding: 0.5rem 1rem;
    border-radius: var(--radius-sm);
    cursor: pointer;
    font-size: 0.875rem;
    transition: all 0.2s ease;
}

.scenarios-tabs .tab:hover {
    background: var(--purple-700);
}

.scenarios-tabs .tab.active {
    background: var(--purple-600);
    color: var(--white);
}

/* Constraints view */
.constraints-view {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
}

.constraint-card {
    background: var(--purple-800);
    border-radius: var(--radius-md);
    padding: 0.75rem;
}

.constraint-card.any-result {
    border-left: 3px solid var(--green-500);
}

.constraint-match {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
}

.constraint-match .home-team,
.constraint-match .away-team {
    font-weight: 600;
    color: var(--white);
}

.constraint-match .vs {
    color: var(--purple-400);
    font-size: 0.75rem;
}

.constraint-requirement {
    display: flex;
    align-items: center;
    gap: 0.375rem;
}

.need-label {
    color: var(--purple-400);
    font-size: 0.875rem;
}

.need-value {
    color: var(--white);
    font-size: 0.875rem;
}

.any-result-badge {
    background: var(--green-600);
    color: var(--white);
    padding: 0.25rem 0.5rem;
    border-radius: 0.25rem;
    font-size: 0.75rem;
    font-weight: 600;
}

.excluded-scores {
    margin-top: 0.5rem;
    padding-top: 0.5rem;
    border-top: 1px solid var(--purple-700);
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.375rem;
}

.excluded-label {
    color: var(--red-400);
    font-size: 0.75rem;
}

.excluded-score {
    background: var(--red-900);
    color: var(--red-200);
    padding: 0.125rem 0.375rem;
    border-radius: 0.25rem;
    font-size: 0.75rem;
}

.excluded-score .reason {
    color: var(--red-300);
    font-size: 0.625rem;
    margin-left: 0.25rem;
}

/* Scenarios list view */
.scenarios-filter {
    display: flex;
    gap: 1rem;
    margin-bottom: 0.75rem;
    flex-wrap: wrap;
}

.scenarios-filter label {
    display: flex;
    align-items: center;
    gap: 0.375rem;
    color: var(--purple-200);
    font-size: 0.875rem;
    cursor: pointer;
}

.scenarios-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

.scenario-card {
    background: var(--purple-800);
    border-radius: var(--radius-sm);
    padding: 0.625rem;
}

.scenario-header {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
}

.scenario-id {
    color: var(--purple-400);
    font-size: 0.75rem;
}

.scenario-badge {
    font-size: 0.625rem;
    font-weight: 600;
    padding: 0.125rem 0.375rem;
    border-radius: 0.25rem;
}

.scenario-badge.win {
    background: var(--green-600);
    color: var(--white);
}

.scenario-badge.tie {
    background: var(--blue-500);
    color: var(--purple-1000);
}

.scenario-badge.monthly {
    border: 1px solid var(--gold);
}

.scenario-results {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
}

.scenario-result {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    background: var(--purple-700);
    padding: 0.25rem 0.375rem;
    border-radius: 0.25rem;
    font-size: 0.75rem;
}

.scenario-result.generic {
    border: 1px dashed var(--purple-500);
}

.scenario-result .team-abbrev {
    color: var(--purple-300);
}

.scenario-result .score {
    color: var(--white);
    font-weight: 600;
}

.scenario-points {
    margin-top: 0.5rem;
    font-size: 0.75rem;
    color: var(--purple-300);
}

.scenario-points .monthly-points {
    color: var(--gold);
    margin-left: 0.25rem;
}

.scenarios-more {
    text-align: center;
    padding: 0.75rem;
    color: var(--purple-300);
    font-size: 0.875rem;
}

.scenarios-more button {
    background: var(--purple-600);
    border: none;
    color: var(--white);
    padding: 0.375rem 0.75rem;
    border-radius: var(--radius-sm);
    margin-left: 0.5rem;
    cursor: pointer;
}

/* Loading and error states */
.modal-loading,
.modal-error {
    padding: 2rem;
    text-align: center;
    color: var(--purple-300);
}

.modal-loading .spinner {
    width: 1.5rem;
    height: 1.5rem;
    border: 2px solid var(--purple-600);
    border-top-color: var(--purple-300);
    border-radius: 50%;
    animation: spin 1s linear infinite;
    margin: 0 auto 0.5rem;
}

.no-scenarios {
    text-align: center;
    color: var(--purple-400);
    padding: 2rem;
}
```

### Step 4: Update app.css to Import New Styles

```css
/* In app.css, add import */
@import 'components/scenarios-modal.css';
```

### Step 5: Integrate Modal into League Dashboard

```razor
@* In LeagueDashboard.razor *@

<LeagueInsights
    LeagueId="@LeagueId"
    CurrentUserId="@CurrentUserId"
    OnContenderSelected="HandleContenderSelected" />

<ScenariosModal
    @bind-IsVisible="ShowScenariosModal"
    LeagueId="@LeagueId"
    Contender="@SelectedContender"
    CurrentUserId="@CurrentUserId" />

@code {
    private bool ShowScenariosModal { get; set; }
    private ContenderInsights? SelectedContender { get; set; }

    private void HandleContenderSelected(ContenderInsights contender)
    {
        SelectedContender = contender;
        ShowScenariosModal = true;
    }
}
```

## Code Patterns to Follow

Follow existing modal patterns in the codebase. Modals typically use:
- Overlay with click-to-close
- `@onclick:stopPropagation` on modal content
- Two-way binding for visibility state

## Verification

- [ ] Modal opens when clicking on a contender or "View your winning scenarios"
- [ ] Modal loads detailed scenarios from API
- [ ] Constraints tab shows what results user needs for each match
- [ ] Scenarios tab shows all winning scenarios with pagination
- [ ] Filters work correctly (round wins, month wins, both)
- [ ] Modal closes on overlay click or close button
- [ ] Responsive design works on mobile
- [ ] Loading and error states display correctly
- [ ] Generic results (H/D/A) display differently from exact scores

## Edge Cases to Consider

- User with many winning scenarios (pagination)
- User with no winning scenarios (eliminated mid-calculation)
- All matches are "any result works" (simple display)
- Very long team names (abbreviation/truncation)
- Slow API response (loading state)
- Network error (error state with retry)

## Notes

- The modal fetches detailed scenarios on demand (not included in summary)
- Scenarios are paginated client-side (50 at a time) to avoid overwhelming the UI
- Generic results show as H/D/A instead of specific scores since they represent "any other" result
- The constraint `GetDescription()` method should be implemented on the DTO (see Task 1)
