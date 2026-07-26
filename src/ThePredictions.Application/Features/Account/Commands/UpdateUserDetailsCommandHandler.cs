using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Account.Commands;

public class UpdateUserDetailsCommandHandler(IUserManager userManager, IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateUserDetailsCommand>
{
    public async Task Handle(UpdateUserDetailsCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, "User");

        user.UpdateDetails(request.FirstName, request.LastName, request.PhoneNumber);
        user.SetMarketingOptIn(request.MarketingOptIn, dateTimeProvider.UtcNow);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new IdentityUpdateException(result.Errors);
    }
}