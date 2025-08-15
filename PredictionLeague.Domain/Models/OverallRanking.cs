namespace PredictionLeague.Domain.Models;

public record OverallRanking(int Rank, int Score, List<LeagueMember> Members);