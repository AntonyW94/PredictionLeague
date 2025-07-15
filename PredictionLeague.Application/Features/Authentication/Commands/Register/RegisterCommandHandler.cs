using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Authentication.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return new RegisterResponse { IsSuccess = false, Message = "User with this email already exists." };

        var newUser = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email
        };

        var result = await _userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return new RegisterResponse { IsSuccess = false, Message = $"User creation failed: {errors}" };
        }

        await _userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));

        return new RegisterResponse { IsSuccess = true, Message = "User created successfully." };
    }
}
