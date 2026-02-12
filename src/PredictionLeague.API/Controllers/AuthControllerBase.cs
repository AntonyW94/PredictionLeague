using Microsoft.AspNetCore.Mvc;

namespace PredictionLeague.API.Controllers;

[ApiController]
public abstract class AuthControllerBase : ApiControllerBase
{
    private readonly IConfiguration _configuration;

    protected AuthControllerBase(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected void SetTokenCookie(string token)
    {
        var expiryDays = double.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]!);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(expiryDays),
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Domain = ".thepredictions.co.uk"
        };

        try
        {
            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to set 'refreshToken' cookie.", ex);
        }
    }
}