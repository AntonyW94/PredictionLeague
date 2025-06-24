using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.API.Contracts.Auth;
using PredictionLeague.Core.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _configuration = configuration;
            _roleManager = roleManager;
        }

        // POST: api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var userExists = await _userManager.FindByEmailAsync(request.Email);
            if (userExists != null)
            {
                return BadRequest(new AuthResponse { IsSuccess = false, Message = "User with this email already exists." });
            }

            var newUser = new ApplicationUser
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                UserName = request.Email // Use email as username
            };

            var result = await _userManager.CreateAsync(newUser, request.Password);

            if (!result.Succeeded)
            {
                // In a real app, you might want to format these errors better.
                return BadRequest(new AuthResponse { IsSuccess = false, Message = "User creation failed.", Token = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }

            const string defaultRole = "Player";
            
            if (await _roleManager.RoleExistsAsync(defaultRole))
                await _userManager.AddToRoleAsync(newUser, defaultRole);

            return Ok(new AuthResponse { IsSuccess = true, Message = "User created successfully." });
        }

        // POST: api/auth/login
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
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["Secret"];
                var issuer = jwtSettings["Issuer"];
                var audience = jwtSettings["Audience"];

                var userRoles = await _userManager.GetRolesAsync(user);

                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id), // 'sub' is a standard claim for user ID
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token ID
                    // Add custom claims like FirstName if needed
                    new Claim("FirstName", user.FirstName),
                    new Claim("LastName", user.LastName),
                    new Claim("FullName", $"{user.FirstName} {user.LastName}")
                }.Union(userRoles.Select(role => new Claim(ClaimTypes.Role, role))); // Add roles to the token claims;

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var expires = DateTime.UtcNow.AddDays(7); // Token expiry date

                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: expires,
                    signingCredentials: creds
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
}
