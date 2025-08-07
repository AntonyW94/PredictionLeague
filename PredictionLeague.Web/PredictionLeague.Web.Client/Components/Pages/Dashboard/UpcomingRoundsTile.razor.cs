using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Web.Client.Services.Browser;
using PredictionLeague.Web.Client.Services.Dashboard;

namespace PredictionLeague.Web.Client.Components.Pages.Dashboard;

public partial class UpcomingRoundsTile : IAsyncDisposable
{
    [Inject] private IDashboardStateService DashboardState { get; set; } = null!;
    [Inject] private IBrowserService BrowserService { get; set; } = null!;

    private int _currentRoundIndex;
    private string _trackStyle = "";
    private double _touchStartX;
    private bool _isDesktop;
    private List<UpcomingRoundDto> _rounds = new();
    
    private int NumberOfDots
    {
        get
        {
            if (!DashboardState.UpcomingRounds.Any())
                return 0;
            
            var roundsPerPage = _isDesktop ? 2 : 1;
            
            if (DashboardState.UpcomingRounds.Count <= roundsPerPage) 
                return 0;
            
            return DashboardState.UpcomingRounds.Count - roundsPerPage + 1;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        DashboardState.OnStateChange += StateHasChanged;
        _isDesktop = await BrowserService.IsDesktop();
        
        _rounds = DashboardState.UpcomingRounds; 
        
        if (!DashboardState.UpcomingRounds.Any())
            await DashboardState.LoadUpcomingRoundsAsync();
    }

    private void HandleTouchStart(TouchEventArgs e)
    {
        _touchStartX = e.Touches[0].ClientX;
    }

    private void HandleTouchMove(TouchEventArgs e)
    {
        
    }

    private void HandleTouchEnd(TouchEventArgs e)
    {
        var touchEndX = e.ChangedTouches[0].ClientX;
        var diffX = _touchStartX - touchEndX;

        if (Math.Abs(diffX) > 50) 
        {
            if (diffX > 0) 
                ShowNext();
            else
                ShowPrevious();
        }
    }

    private void GoToSlide(int index)
    {
        if (index >= 0 && index < NumberOfDots)
        {
            _currentRoundIndex = index;
            UpdateTrackStyle();
        }
    }

    private void ShowPrevious()
    {
        if (_currentRoundIndex > 0)
        {
            _currentRoundIndex--;
            UpdateTrackStyle();
        }
    }

    private void ShowNext()
    {
        if (!IsAtEnd())
        {
            _currentRoundIndex++;
            UpdateTrackStyle();
        }
    }

    private void UpdateTrackStyle()
    {
        var percentageToMove = _isDesktop ? 50 : 100;
        _trackStyle = $"transform: translateX(-{_currentRoundIndex * percentageToMove}%);";
    }

    private bool IsAtEnd()
    {
        var roundsPerPage = _isDesktop ? 2 : 1;
        return _currentRoundIndex >= _rounds.Count - roundsPerPage;
    }

    public ValueTask DisposeAsync()
    {
        DashboardState.OnStateChange -= StateHasChanged;
        return ValueTask.CompletedTask;
    }
}