using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
public record SeasonTeamDto(string Name, string? LogoUrl);
