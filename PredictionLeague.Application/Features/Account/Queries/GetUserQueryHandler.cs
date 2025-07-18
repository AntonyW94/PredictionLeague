using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Contracts.Account;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Account.Queries;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDetails?>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserQueryHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDetails?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            return null;

        return new UserDetails
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        };
    }
}