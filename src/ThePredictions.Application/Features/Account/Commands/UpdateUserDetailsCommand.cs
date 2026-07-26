using MediatR;

namespace ThePredictions.Application.Features.Account.Commands;

public record UpdateUserDetailsCommand(
    string UserId,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool MarketingOptIn) : IRequest;