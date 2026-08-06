using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateLeagueCommand(
    int Id,
    string Name,
    decimal Price,
    DateTime EntryDeadlineUtc,
    int PointsForExactScore,
    int PointsForCorrectResult,
    string UserId,
    string? BankAccountName = null,
    string? BankSortCode = null,
    string? BankAccountNumber = null,
    string? PaymentReferenceTemplate = null,
    decimal? PrizeFundOverride = null,
    bool RequiresMemberApproval = true,
    bool IsListed = false) : IRequest;
