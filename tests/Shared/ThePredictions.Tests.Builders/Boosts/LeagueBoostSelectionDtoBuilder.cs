using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Tests.Builders.Boosts;

public class LeagueBoostSelectionDtoBuilder
{
    private string _boostCode = "DOUBLE_UP";
    private bool _isEnabled = true;
    private int _totalUsesPerSeason = 3;
    private List<BoostWindowSelectionDto> _windows = [new BoostWindowSelectionDtoBuilder().Build()];

    public LeagueBoostSelectionDtoBuilder WithBoostCode(string boostCode)
    {
        _boostCode = boostCode;
        return this;
    }

    public LeagueBoostSelectionDtoBuilder WithTotalUsesPerSeason(int totalUsesPerSeason)
    {
        _totalUsesPerSeason = totalUsesPerSeason;
        return this;
    }

    public LeagueBoostSelectionDtoBuilder WithWindows(List<BoostWindowSelectionDto> windows)
    {
        _windows = windows;
        return this;
    }

    public LeagueBoostSelectionDto Build() => new()
    {
        BoostCode = _boostCode,
        IsEnabled = _isEnabled,
        TotalUsesPerSeason = _totalUsesPerSeason,
        Windows = _windows
    };
}
