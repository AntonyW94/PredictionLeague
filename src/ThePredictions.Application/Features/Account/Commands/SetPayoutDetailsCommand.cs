using MediatR;

namespace ThePredictions.Application.Features.Account.Commands;

public record SetPayoutDetailsCommand(
    string UserId,
    string? AccountName,
    string? SortCode,
    string? AccountNumber) : IRequest;
