using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

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
    decimal? PrizeFundOverride = null) : IRequest;