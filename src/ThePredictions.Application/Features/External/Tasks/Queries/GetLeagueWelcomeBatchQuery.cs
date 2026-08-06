using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>
/// Finds leagues whose entry deadline passed between <paramref name="WindowStartUtc"/> and
/// <paramref name="NowUtc"/> and that still have approved members who haven't received the welcome
/// email. Leagues with a prize scheme that has not yet been frozen into settings are excluded - the
/// next hourly scan picks them up after the freeze, so the email always shows confirmed prizes.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeagueWelcomeBatchQuery(DateTime NowUtc, DateTime WindowStartUtc) : IRequest<IReadOnlyList<LeagueWelcomeLeague>>;
