using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Tests.Builders.Admin.Seasons;

public class CreateSeasonRequestBuilder
{
    private string _name = "Premier League 2025-26";
    private DateTime _startDateUtc = new(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime _endDateUtc = new(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);
    private int _numberOfRounds = 38;
    private int _competitionId = 1;

    public CreateSeasonRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CreateSeasonRequestBuilder WithStartDateUtc(DateTime startDateUtc)
    {
        _startDateUtc = startDateUtc;
        return this;
    }

    public CreateSeasonRequestBuilder WithEndDateUtc(DateTime endDateUtc)
    {
        _endDateUtc = endDateUtc;
        return this;
    }

    public CreateSeasonRequestBuilder WithNumberOfRounds(int numberOfRounds)
    {
        _numberOfRounds = numberOfRounds;
        return this;
    }

    public CreateSeasonRequestBuilder WithCompetitionId(int competitionId)
    {
        _competitionId = competitionId;
        return this;
    }

    public CreateSeasonRequest Build() => new()
    {
        Name = _name,
        StartDateUtc = _startDateUtc,
        EndDateUtc = _endDateUtc,
        NumberOfRounds = _numberOfRounds,
        CompetitionId = _competitionId
    };
}
