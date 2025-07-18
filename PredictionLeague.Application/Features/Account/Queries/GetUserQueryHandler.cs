using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PredictionLeague.Contracts.Account;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Account.Queries;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDetails?>
{
    private readonly ILogger<GetUserQueryHandler> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserQueryHandler(ILogger<GetUserQueryHandler> logger, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<UserDetails?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user != null)
        {
            return new UserDetails
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            };
        }

        _logger.LogInformation("Details requested for non-existent User (ID: {UserId}).", request.UserId);
        return null;

    }
}