using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Account.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record SetPayoutDetailsCommand(
    string UserId,
    string? AccountName,
    string? SortCode,
    string? AccountNumber) : IRequest;
