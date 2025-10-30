using PredictionLeague.Contracts.Admin.Users;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services;

public interface IReminderService
{
    Task<bool> ShouldSendReminderAsync(Round round, DateTime now, CancellationToken cancellationToken);
    Task<List<ChaseUserDto>> GetUsersMissingPredictionsAsync(int roundId, CancellationToken cancellationToken);
}