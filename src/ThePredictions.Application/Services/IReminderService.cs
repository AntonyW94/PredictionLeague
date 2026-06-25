using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public interface IReminderService
{
    Task<bool> ShouldSendReminderAsync(Round round, DateTime nowUtc, CancellationToken cancellationToken);
    Task<List<ChaseUserDto>> GetUsersMissingPredictionsAsync(int roundId, DateTime nowUtc, CancellationToken cancellationToken);
}