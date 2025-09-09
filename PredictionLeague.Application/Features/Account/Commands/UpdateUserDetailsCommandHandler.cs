using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Common.Exceptions;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Account.Commands;

public class UpdateUserDetailsCommandHandler : IRequestHandler<UpdateUserDetailsCommand>
{
    private readonly IUserManager _userManager;

    public UpdateUserDetailsCommandHandler(IUserManager userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(UpdateUserDetailsCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, "User");

        user.UpdateDetails(request.FirstName, request.LastName, request.PhoneNumber);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new IdentityUpdateException(result.Errors);
    }
}