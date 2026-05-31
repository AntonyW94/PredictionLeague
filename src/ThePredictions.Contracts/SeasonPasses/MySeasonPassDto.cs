namespace ThePredictions.Contracts.SeasonPasses;

public record MySeasonPassDto(
    int SeasonId,
    string SeasonName,
    string Tier,
    string Source,
    decimal AmountPaid,
    bool HasSmsReminders,
    DateTime CreatedAtUtc);
