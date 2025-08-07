using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PredictionLeague.Contracts.Leaderboards;

public class LeagueLeaderboardDto
{
    public int LeagueId { get; set; }
    public string LeagueName { get; set; }
    public string SeasonName { get; set; }
    public IEnumerable<LeaderboardEntryDto> Entries { get; set; } = new List<LeaderboardEntryDto>();
}