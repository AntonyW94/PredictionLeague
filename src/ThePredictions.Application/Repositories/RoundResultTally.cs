using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Repositories;

/// <summary>One player's tally for a round, ready to be stored.</summary>
public sealed record RoundResultTally(string UserId, OutcomeCounts Counts);
