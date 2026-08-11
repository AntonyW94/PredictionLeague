using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The league and season details the prize page shows above its prize list.
/// </summary>
/// <remarks>
/// <see cref="EntryDeadlineUtc"/> is nullable because the column is. The old result type declared it non-nullable, which
/// would have failed to materialise for a league with no deadline - latent rather than live, since the create and update
/// commands both require one, but it is honest here and the handler decides what "none" looks like.
///
/// Two member counts, and the handler uses the total. That preserves today's figure, which counts pending and rejected
/// requests towards the prize pot preview; the plan document records the question, as it does for the league settings
/// page that does the same thing.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaguePrizesHeaderRow(
    string LeagueName,
    DateTime? EntryDeadlineUtc,
    decimal Price,
    int TotalMembershipCount,
    int ApprovedMemberCount,
    int NumberOfRounds,
    DateTime SeasonStartDateUtc,
    DateTime SeasonEndDateUtc);
