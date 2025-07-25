﻿namespace PredictionLeague.Contracts.Admin.Seasons;

public record SeasonDto(
    int Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    int NumberOfRounds, 
    int RoundCount
) : SeasonLookupDto(Id, Name, StartDate);