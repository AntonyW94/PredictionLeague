using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>A league that has a prize scheme attached to it.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WelcomeSchemeRow(int LeagueId);
