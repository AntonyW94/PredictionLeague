using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>One welcome email that has already been sent.</summary>
/// <remarks>
/// The whole point of this set. Sending somebody the same welcome twice is the failure this job has to avoid, so the check is
/// written out and tested rather than left as a <c>NOT EXISTS</c> inside the read.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WelcomeNotificationRow(int LeagueId, string UserId);
