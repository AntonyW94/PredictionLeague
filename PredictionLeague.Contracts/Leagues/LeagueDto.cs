﻿namespace PredictionLeague.Contracts.Leagues;

public record LeagueDto(
    int Id,
    string Name,
    string SeasonName,
    int MemberCount,
    decimal Price,
    string EntryCode,
    DateTime EntryDeadline
);