using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One prize a player has already been emailed about.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeNotificationRow(string UserId, int LeaguePrizeSettingId, int? RoundNumber, int? Month);
