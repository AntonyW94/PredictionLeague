using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using PredictionLeague.Shared.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IDbConnectionFactory connectionFactory, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
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
        
        await _userManager.AddToRoleAsync(newUser, nameof(ApplicationUserRole.Player));
        return Ok(new AuthResponse { IsSuccess = true, Message = "User created successfully." });

    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new AuthResponse { IsSuccess = false, Message = "Invalid email or password." });

        var refreshToken = await GenerateAndStoreRefreshToken(user);
        SetTokenCookie(refreshToken.Token);

        return Ok(await GenerateAccessToken(user));
    }

    [HttpGet("google-login")]
    [AllowAnonymous]
    public IActionResult GoogleLogin([FromQuery] string returnUrl)
    {
        var callbackUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = callbackUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("signin-google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(string returnUrl)
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
            {
                _logger.LogWarning("Google authentication failed. The external cookie was not found or was invalid.");
                return Redirect($"{returnUrl}?error=Authentication with external provider failed.");
            }

            var providerKey = authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(providerKey))
            {
                _logger.LogWarning("Could not find NameIdentifier claim from external provider.");
                return Redirect($"{returnUrl}?error=Could not determine user identifier from external provider.");
            }

            if (!authenticateResult.Properties.Items.TryGetValue(".AuthScheme", out var provider))
            {
                _logger.LogWarning("Could not find .AuthScheme in authentication properties.");
                return Redirect($"{returnUrl}?error=Could not determine login provider details.");
            }

            if (string.IsNullOrEmpty(provider))
            {
                _logger.LogWarning(".AuthScheme in authentication properties was null.");
                return Redirect($"{returnUrl}?error=.AuthScheme in authentication properties was null.");
            }
            
            var externalLoginInfo = new ExternalLoginInfo(authenticateResult.Principal, provider, providerKey, provider);

            var user = await _userManager.FindByLoginAsync(externalLoginInfo.LoginProvider, externalLoginInfo.ProviderKey);
            if (user == null)
            {
                var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(email))
                    return Redirect($"{returnUrl}?error=Could not retrieve email from Google.");

                user = await _userManager.FindByEmailAsync(email);
              
                if (user == null)
                {
                    user = new ApplicationUser { UserName = email, Email = email, FirstName = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "", LastName = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Surname) ?? "", EmailConfirmed = true };
                    var createResult = await _userManager.CreateAsync(user);
                    if (createResult.Succeeded)
                        await _userManager.AddToRoleAsync(user, nameof(ApplicationUserRole.Player));
                    else
                        return Redirect($"{returnUrl}?error=There was a problem creating your account.");
                }
                
                await _userManager.AddLoginAsync(user, externalLoginInfo);
            }

            var refreshToken = await GenerateAndStoreRefreshToken(user);
            SetTokenCookie(refreshToken.Token);
           
            var script = $"<html><body><script>window.location = '{returnUrl}';</script></body></html>";
            
            return new ContentResult
            {
                Content = script,
                ContentType = "text/html"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during Google callback processing.");
            return Redirect($"{returnUrl}?error=An unexpected error occurred. Please try again.");
        }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new AuthResponse { Message = "Refresh token not found." });

        using var connection = _connectionFactory.CreateConnection();

        var storedToken = await connection.QuerySingleOrDefaultAsync<RefreshToken>("SELECT * FROM RefreshTokens WHERE Token = @Token", new { Token = refreshToken });
        if (storedToken is not { IsActive: true })
            return Unauthorized(new AuthResponse { Message = "Invalid or expired refresh token." });

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
            return Unauthorized(new AuthResponse { Message = "User not found." });

        return Ok(await GenerateAccessToken(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];

        if (!string.IsNullOrEmpty(refreshToken))
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync("UPDATE RefreshTokens SET Revoked = @Revoked WHERE Token = @Token AND Revoked IS NULL", new { Revoked = DateTime.UtcNow, Token = refreshToken });
        }

        Response.Cookies.Delete("refreshToken");

        return Ok(new { message = "Logged out successfully." });
    }

    private void SetTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            Secure = true,
            SameSite = SameSiteMode.Strict
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }

    private async Task<RefreshToken> GenerateAndStoreRefreshToken(ApplicationUser user)
    {
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("INSERT INTO RefreshTokens (UserId, Token, Expires, Created) VALUES (@UserId, @Token, @Expires, @Created)", refreshToken);

        return refreshToken;
    }

    private async Task<AuthResponse> GenerateAccessToken(ApplicationUser user)
    {
        var userRoles = await _userManager.GetRolesAsync(user);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? throw new Exception("User Email is Null when Generating Access Token")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            new Claim("FullName", $"{user.FirstName} {user.LastName}")
        }.Union(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var jwtSecret = jwtSettings["Secret"];
        if (jwtSecret == null)
            throw new Exception("JWT Secret Not Found");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        var expires = DateTime.UtcNow.AddMinutes(15);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JwtSecurityTokenHandler().CreateToken(tokenDescriptor);

        return new AuthResponse
        {
            IsSuccess = true,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expires
        };
    }
}