using MediatR;

namespace PredictionLeague.Application.Features.Account.Commands;

public record UpdateUserDetailsCommand(
    string UserId, 
    string FirstName, 
    string LastName, 
    string? PhoneNumber) : IRequest;