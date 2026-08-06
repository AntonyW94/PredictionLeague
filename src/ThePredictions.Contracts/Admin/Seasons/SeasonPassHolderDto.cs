using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SeasonPassHolderDto(
    string UserId,
    string FullName,
    string Email,
    SeasonPassTier Tier,
    SeasonPassSource Source,
    decimal AmountPaid,
    decimal SmsFeePaid,
    DateTime AcquiredAtUtc
);
