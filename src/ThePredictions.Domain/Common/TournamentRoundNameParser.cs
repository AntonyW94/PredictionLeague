using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Common;

public static class TournamentRoundNameParser
{
    private static readonly Dictionary<string, TournamentStage> KnockoutMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Round of 32"] = TournamentStage.RoundOf32,
        ["Round of 16"] = TournamentStage.RoundOf16,
        ["Quarter-finals"] = TournamentStage.QuarterFinals,
        ["Semi-finals"] = TournamentStage.SemiFinals,
        ["3rd Place Final"] = TournamentStage.ThirdPlace,
        ["Final"] = TournamentStage.Final
    };

    public static bool TryParseStage(string apiRoundName, out TournamentStage stage)
    {
        stage = default;

        if (string.IsNullOrWhiteSpace(apiRoundName))
            return false;

        // Group stage: "Group Stage - 1", "Group Stage - 2", "Group Stage - 3"
        if (apiRoundName.StartsWith("Group Stage - ", StringComparison.OrdinalIgnoreCase))
        {
            var matchdayPart = apiRoundName["Group Stage - ".Length..];
            if (int.TryParse(matchdayPart, out var matchday))
            {
                stage = matchday switch
                {
                    1 => TournamentStage.Group1,
                    2 => TournamentStage.Group2,
                    3 => TournamentStage.Group3,
                    _ => default
                };
                return matchday >= 1 && matchday <= 3;
            }

            return false;
        }

        // Knockout stages: exact name match
        return KnockoutMappings.TryGetValue(apiRoundName, out stage);
    }

    public static bool IsKnockoutStage(TournamentStage stage)
    {
        return stage is not (TournamentStage.Group1 or TournamentStage.Group2 or TournamentStage.Group3);
    }

    /// <summary>
    /// The prediction "batch" a stage belongs to within a combined round. Matches in the same batch have
    /// their teams decided by the same earlier matches, so they become known together and lock together
    /// 30 minutes before the earliest fixture in the batch. Every stage is its own batch except the Third
    /// Place Playoff and the Final, which are both decided by the Semi-finals and therefore share a batch.
    /// The value increases with tournament depth, so batches order naturally from earliest to latest.
    /// </summary>
    public static int GetPredictionBatch(TournamentStage stage)
    {
        // The Final is decided by the same matches (the Semi-finals) as the Third Place Playoff, so it
        // locks in the same batch rather than 30 minutes before its own (later) kickoff.
        return stage == TournamentStage.Final
            ? (int)TournamentStage.ThirdPlace
            : (int)stage;
    }

    public static int CalculateExpectedMatchCount(TournamentStage stage, int totalTeams)
    {
        return stage switch
        {
            TournamentStage.Group1 or TournamentStage.Group2 or TournamentStage.Group3 => totalTeams / 2,
            TournamentStage.RoundOf32 => 16,
            TournamentStage.RoundOf16 => 8,
            TournamentStage.QuarterFinals => 4,
            TournamentStage.SemiFinals => 2,
            TournamentStage.ThirdPlace => 1,
            TournamentStage.Final => 1,
            _ => 0
        };
    }

    public static string GetDefaultDisplayName(TournamentStage stage)
    {
        return stage switch
        {
            TournamentStage.Group1 => "Group Stage - Matchday 1",
            TournamentStage.Group2 => "Group Stage - Matchday 2",
            TournamentStage.Group3 => "Group Stage - Matchday 3",
            TournamentStage.RoundOf32 => "Round of 32",
            TournamentStage.RoundOf16 => "Round of 16",
            TournamentStage.QuarterFinals => "Quarter-finals",
            TournamentStage.SemiFinals => "Semi-finals",
            TournamentStage.ThirdPlace => "Third Place Playoff",
            TournamentStage.Final => "Final",
            _ => stage.ToString()
        };
    }

    public static string GetCombinedDisplayName(IReadOnlyList<TournamentStage> stages)
    {
        if (stages.Count == 0)
            return string.Empty;

        if (stages.Count == 1)
            return GetDefaultDisplayName(stages[0]);

        var names = stages.Select(GetDefaultDisplayName).ToList();
        return names.Count == 2
            ? $"{names[0]} & {names[1]}"
            : $"{string.Join(", ", names.Take(names.Count - 1))} & {names[^1]}";
    }

    public static string GetPlaceholderMatchName(TournamentStage stage, int matchNumber)
    {
        return stage switch
        {
            TournamentStage.Group1 or TournamentStage.Group2 or TournamentStage.Group3
                => $"Group Match {matchNumber}",
            TournamentStage.RoundOf32 => $"R32 Match {matchNumber}",
            TournamentStage.RoundOf16 => $"R16 Match {matchNumber}",
            TournamentStage.QuarterFinals => $"QF {matchNumber}",
            TournamentStage.SemiFinals => $"Semi-final {matchNumber}",
            TournamentStage.ThirdPlace => "Third Place Playoff",
            TournamentStage.Final => "Final",
            _ => $"Match {matchNumber}"
        };
    }
}
