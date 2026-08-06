using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Rounds;

/// <summary>The players an admin or league owner has asked to remind about a round.</summary>
[ExcludeFromCodeCoverage]
public record SendPredictionRemindersRequest(List<string> UserIds);
