using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>One pass holder on the administrator's page.</summary>
/// <remarks>
/// The full name is composed by the read here rather than in C#, because it is also what the name filter searches and
/// what the sort orders by - and those have to agree with what is displayed or the page contradicts itself.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonPassHolderRow(
    string UserId,
    string FullName,
    string Email,
    SeasonPassTier Tier,
    SeasonPassSource Source,
    decimal AmountPaid,
    decimal SmsFeePaid,
    DateTime CreatedAtUtc);
