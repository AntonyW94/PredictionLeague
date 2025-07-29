namespace PredictionLeague.Contracts.Dashboard;

public record UpcomingRoundDto(
    int Id,
    string SeasonName, 
    int RoundNumber, 
    DateTime Deadline,
    bool HasUserPredicted);