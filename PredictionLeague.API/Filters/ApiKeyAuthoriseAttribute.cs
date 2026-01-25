using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PredictionLeague.API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthoriseAttribute : Attribute, IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedApiKey = configuration["FootballApi:SchedulerApiKey"];

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var potentialApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (string.IsNullOrEmpty(expectedApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Use constant-time comparison to prevent timing attacks
        if (!ConstantTimeEquals(expectedApiKey, potentialApiKey.ToString()))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }

    /// <summary>
    /// Compares two strings in constant time to prevent timing attacks.
    /// </summary>
    private static bool ConstantTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        // If lengths differ, still perform comparison to maintain constant time
        if (expectedBytes.Length != actualBytes.Length)
        {
            CryptographicOperations.FixedTimeEquals(expectedBytes, expectedBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
