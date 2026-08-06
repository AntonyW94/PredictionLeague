using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Tests.Builders.Boosts;

public class BoostWindowSelectionDtoBuilder
{
    private int _startRoundNumber = 1;
    private int _endRoundNumber = 10;
    private int _maxUsesInWindow = 2;

    public BoostWindowSelectionDtoBuilder WithStartRoundNumber(int startRoundNumber)
    {
        _startRoundNumber = startRoundNumber;
        return this;
    }

    public BoostWindowSelectionDtoBuilder WithEndRoundNumber(int endRoundNumber)
    {
        _endRoundNumber = endRoundNumber;
        return this;
    }

    public BoostWindowSelectionDtoBuilder WithMaxUsesInWindow(int maxUsesInWindow)
    {
        _maxUsesInWindow = maxUsesInWindow;
        return this;
    }

    public BoostWindowSelectionDto Build() => new()
    {
        StartRoundNumber = _startRoundNumber,
        EndRoundNumber = _endRoundNumber,
        MaxUsesInWindow = _maxUsesInWindow
    };
}
