using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Sharing.Queries;

/// <summary>The player a share card is for.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ShareCardPlayerRow(string? FirstName, string? PreferredTheme);
