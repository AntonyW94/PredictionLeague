namespace ThePredictions.Contracts.Admin.EmailTests;

public record SendTestEmailResultDto(bool Success, string? MessageId, string? Error, string SentTo);
