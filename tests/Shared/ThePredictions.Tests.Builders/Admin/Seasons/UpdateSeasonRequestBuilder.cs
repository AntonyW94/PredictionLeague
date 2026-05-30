using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Tests.Builders.Admin.Seasons;

public class UpdateSeasonRequestBuilder
{
    private string _name = "Premier League 2025-26";
    private DateTime _startDateUtc = new(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime _endDateUtc = new(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);
    private int _numberOfRounds = 38;
    private int _competitionId = 1;

    public UpdateSeasonRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UpdateSeasonRequestBuilder WithStartDateUtc(DateTime startDateUtc)
    {
        _startDateUtc = startDateUtc;
        return this;
    }

    public UpdateSeasonRequestBuilder WithEndDateUtc(DateTime endDateUtc)
    {
        _endDateUtc = endDateUtc;
        return this;
    }

    public UpdateSeasonRequestBuilder WithNumberOfRounds(int numberOfRounds)
    {
        _numberOfRounds = numberOfRounds;
        return this;
    }

    public UpdateSeasonRequestBuilder WithCompetitionId(int competitionId)
    {
        _competitionId = competitionId;
        return this;
    }

    public UpdateSeasonRequest Build() => new()
    {
        Name = _name,
        StartDateUtc = _startDateUtc,
        EndDateUtc = _endDateUtc,
        NumberOfRounds = _numberOfRounds,
        CompetitionId = _competitionId
    };
}
