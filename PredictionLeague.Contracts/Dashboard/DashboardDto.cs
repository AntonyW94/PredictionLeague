﻿using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Contracts.Dashboard;

public class DashboardDto
{
    public List<UpcomingRoundDto> UpcomingRounds { get; init; } = new();
    public List<PublicLeagueDto> PublicLeagues { get; init; } = new();
}