using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Tests.Builders.Boosts;

public class SetLeagueBoostRulesRequestBuilder
{
    private List<LeagueBoostSelectionDto> _selections = [new LeagueBoostSelectionDtoBuilder().Build()];

    public SetLeagueBoostRulesRequestBuilder WithSelections(List<LeagueBoostSelectionDto> selections)
    {
        _selections = selections;
        return this;
    }

    public SetLeagueBoostRulesRequest Build() => new()
    {
        Selections = _selections
    };
}
