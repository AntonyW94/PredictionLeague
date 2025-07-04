using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILeagueService _leagueService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AuthController(IConfiguration configuration, ILeagueService leagueService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _configuration = configuration;
        _leagueService = leagueService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var userExists = await _userManager.FindByEmailAsync(request.Email);
        if (userExists != null)
            return BadRequest(new AuthResponse { IsSuccess = false, Message = "User with this email already exists." });

        var newUser = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email
        };

        var result = await _userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
            return BadRequest(new AuthResponse { IsSuccess = false, Message = "User creation failed.", Token = string.Join(", ", result.Errors.Select(e => e.Description)) });
           
        var defaultLeague = await _leagueService.GetDefaultPublicLeagueAsync();
        if (defaultLeague != null)
            await _leagueService.JoinPublicLeagueAsync(defaultLeague.Id, newUser.Id);
            
        const ApplicationUserRole defaultRole = ApplicationUserRole.Player;
            
        if (await _roleManager.RoleExistsAsync(defaultRole.ToString()))
            await _userManager.AddToRoleAsync(newUser, defaultRole.ToString());

        return Ok(new AuthResponse { IsSuccess = true, Message = "User created successfully." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new AuthResponse { IsSuccess = false, Message = "Invalid email or password." });
        }

        var tokenResponse = await GenerateJwtToken(user);

        return Ok(tokenResponse);
    }

    private async Task<AuthResponse> GenerateJwtToken(ApplicationUser user)
    {
        try
        {
            if (string.IsNullOrEmpty(user.Email))
                throw new ArgumentException("User email is null or empty, cannot generate a token.", nameof(user));

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var userRoles = await _userManager.GetRolesAsync(user);
            var secretKey = jwtSettings["Secret"];
         
            if (string.IsNullOrEmpty(secretKey))
                throw new InvalidOperationException("JWT Secret key is not configured.");
           
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id), 
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), 
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName),
                new Claim("FullName", $"{user.FirstName} {user.LastName}")
            }.Union(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddDays(7);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            return new AuthResponse
            {
                IsSuccess = true,
                Message = "Login successful.",
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expires
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse { IsSuccess = false, Message = $"Token generation failed: {ex.Message}" };
        }
    }
}