using MediatR;
using ThePredictions.Contracts.Account;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>A player's own account details.</summary>
public class GetUserQueryHandler(IAccountProfileQuery accountProfileQuery) : IRequestHandler<GetUserQuery, UserDetails>
{
    public async Task<UserDetails> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await accountProfileQuery.ExecuteAsync(request.UserId, cancellationToken)
                   ?? throw new EntityNotFoundException("User", request.UserId);

        return new UserDetails(
            user.FirstName,
            user.LastName,
            user.Email,
            user.PhoneNumber,
            user.PreferredTheme,

            // Opting in is recorded as the moment it happened, so having a date is what "yes" means. The screen only needs the
            // answer, but storing the date is what lets the consent be evidenced later.
            MarketingOptIn: user.MarketingOptInAtUtc is not null);
    }
}
