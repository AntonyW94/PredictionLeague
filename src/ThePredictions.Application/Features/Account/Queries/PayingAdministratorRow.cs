using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>One administrator who pays prizes in a league this player is in.</summary>
/// <remarks>
/// Both name parts, because composing and ordering them is a rule - and this screen wants the full name rather than the
/// abbreviated one players see, since it is telling somebody who will be sending them money.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PayingAdministratorRow(string UserId, string? FirstName, string? LastName);
