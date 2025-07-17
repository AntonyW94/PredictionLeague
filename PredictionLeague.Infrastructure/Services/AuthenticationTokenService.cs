
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Domain.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PredictionLeague.Infrastructure.Services;

public class AuthenticationTokenService : IAuthenticationTokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthenticationTokenService(UserManager<ApplicationUser> userManager, IConfiguration configuration, IDbConnectionFactory connectionFactory)
    {
        _userManager = userManager;
        _configuration = configuration;
        _connectionFactory = connectionFactory;
    }

    public async Task<AuthenticationResponse> GenerateAccessToken(ApplicationUser user)
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
        }.Union(userRoles.Select(role => new Claim("role", role)));

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

        return new AuthenticationResponse
        {
            IsSuccess = true,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expires
        };
    }

    public async Task<RefreshToken> GenerateAndStoreRefreshToken(ApplicationUser user)
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
}
