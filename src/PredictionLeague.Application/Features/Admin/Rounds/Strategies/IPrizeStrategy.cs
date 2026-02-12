using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Strategies;

public interface IPrizeStrategy
{
    PrizeType PrizeType { get; }
    Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken);
}