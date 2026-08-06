using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeagueDashboardMemberDto(string FullName, string Status, DateTime JoinedAtUtc);
