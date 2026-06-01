namespace ThePredictions.Contracts.Authentication;

public class ConfirmEmailRequest
{
    public string Token { get; set; } = string.Empty;
}
