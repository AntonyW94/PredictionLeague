namespace ThePredictions.SchemaCheck;

public sealed record CheckResult(ReadCallSite CallSite, CheckStatus Status, string Detail)
{
    public bool IsFailure => Status is CheckStatus.Mismatch or CheckStatus.Broken;
}
