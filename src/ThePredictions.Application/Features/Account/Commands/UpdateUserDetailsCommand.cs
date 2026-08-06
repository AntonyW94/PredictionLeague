using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Account.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateUserDetailsCommand(
    string UserId,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool MarketingOptIn) : IRequest;
