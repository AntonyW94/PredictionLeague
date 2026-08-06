using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailTests;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SendTestEmailResultDto(bool Success, string? MessageId, string? Error, string SentTo);
