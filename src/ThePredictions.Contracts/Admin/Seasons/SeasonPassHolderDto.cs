using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage]
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
