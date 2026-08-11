using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One social sign-in an account has linked.</summary>
/// <remarks>
/// A row each. The statement this replaces used <c>STRING_AGG(ul.[LoginProvider], ',')</c> and the handler then split the
/// result back apart on the comma - a list flattened into a string by the database and reassembled in C#, which breaks
/// the day a provider name contains a comma.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserLoginProviderRow(string UserId, string LoginProvider);
