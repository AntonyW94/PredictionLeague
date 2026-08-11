using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// Which half of a tournament a round belongs to, derived from its mapping's stage text: anything mentioning a
/// group is the group stage, everything else is knockout.
/// </summary>
/// <remarks>
/// Written out in SQL as <c>CASE WHEN trm.[Stages] LIKE '%Group%' THEN ... ELSE ... END</c> in nine places
/// across six files - and, worse, producing three different spellings of the same stage name between them:
/// <c>'Group Stage'</c>, <c>'Group stage'</c> and <c>"GroupStage"</c>.
///
/// That matters beyond untidiness. <c>LeagueStatsRepository</c> compares its spelling against a stored
/// <c>ActiveStageName</c>, so the variants agree only because the database collation is case-insensitive
/// (<c>SQL_Latin1_General_CP1_CI_AS</c>). On a case-sensitive collation - or a different engine - they would
/// silently stop matching.
///
/// This class is the classification. The stage's <i>display</i> spelling is a separate question, still written
/// out at each of the remaining call sites, and worth settling when those move.
/// </remarks>
public static class TournamentStageClassifier
{
    /// <summary>Classifies a round from the <c>Stages</c> text on its tournament mapping.</summary>
    public static TournamentStageGroup ClassifyFrom(string? stages) =>
        stages != null && stages.Contains("Group", StringComparison.OrdinalIgnoreCase)
            ? TournamentStageGroup.GroupStage
            : TournamentStageGroup.KnockoutStage;
}
