using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>One league of a season, and when entry to it closes.</summary>
/// <remarks>
/// The deadline arrives rather than a yes-or-no answer, because whether entry is still open is measured against the
/// injected clock. It is nullable, and a league with no deadline is not open - a rule the old statements enforced only by
/// accident, since SQL's three-valued logic drops such a row from <c>EntryDeadlineUtc &gt; GETUTCDATE()</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonLeagueEntryRow(int SeasonId, int LeagueId, DateTime? EntryDeadlineUtc);
