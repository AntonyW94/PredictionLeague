using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// What a tournament stage is called on screen.
/// </summary>
/// <remarks>
/// The names were written out at each of the nine sites that classified a round, in three different spellings -
/// <c>'Group Stage'</c>, <c>'Group stage'</c> and <c>"GroupStage"</c> - which agreed only because the database
/// collation ignores case. Two of those sites compare the name against a stored value rather than displaying it,
/// so the spelling is load-bearing there.
///
/// This is the display spelling, paired with <see cref="TournamentStageClassifier"/> so that classifying a round
/// and naming its stage cannot drift apart.
/// </remarks>
public static class TournamentStageName
{
    public static string For(TournamentStageGroup stage) =>
        stage == TournamentStageGroup.GroupStage ? "Group Stage" : "Knockout Stage";
}
