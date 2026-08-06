using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>The live projected prize breakdown at the current entrant count, for members and the admin.</summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeaguePrizeBreakdownQuery(int LeagueId, string CurrentUserId) : IRequest<PrizeBreakdownDto>;
