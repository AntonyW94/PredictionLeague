using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

/// <summary>One football team.</summary>
/// <remarks>
/// <see cref="LogoUrl"/> is nullable because the column is. Two of the three reads of this table declared it as
/// never-null, which Dapper honours by writing the null in anyway; the third had it right.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record TeamRow(
    int Id,
    string Name,
    string ShortName,
    string? LogoUrl,
    string Abbreviation,
    int? ApiTeamId);
