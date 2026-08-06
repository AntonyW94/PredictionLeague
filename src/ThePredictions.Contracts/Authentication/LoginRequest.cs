using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // When true, the refresh-token cookie is persistent (survives browser restart);
    // when false it is a session cookie, cleared when the browser closes.
    public bool RememberMe { get; set; }
}
