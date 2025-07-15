namespace PredictionLeague.Contracts.Auth;

public class RegisterResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
}