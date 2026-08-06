using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailTests;

[ExcludeFromCodeCoverage]
public record SendTestEmailResultDto(bool Success, string? MessageId, string? Error, string SentTo);
