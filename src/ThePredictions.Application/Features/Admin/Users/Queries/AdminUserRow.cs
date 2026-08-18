using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One account, as the administrator's list identifies it.</summary>
/// <remarks>
/// Both name parts arrive raw, because composing them is a rule. <see cref="HasPassword"/> is whether a password hash is
/// stored - the hash itself never leaves the database - and what that means to the screen, an account that can sign in
/// without a social provider, is the handler's to say.
///
/// The two consent columns arrive as the dates they are stored as rather than as flags. Whether a date means consent was
/// given is not in doubt, but the date is the part that would answer a subject access request, so the read does not throw
/// it away to save the screen a comparison.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdminUserRow(
    string Id,
    string? FirstName,
    string? LastName,
    string Email,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool HasPassword,
    bool IsAdmin,
    DateTime? TermsAcceptedAtUtc,
    DateTime? MarketingOptInAtUtc);
