namespace PredictionLeague.Contracts.Authentication;

public class RegisterResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
}