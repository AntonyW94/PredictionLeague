using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public record LeagueDashboardMemberDto(string FullName, string Status, DateTime JoinedAtUtc);
