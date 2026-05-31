namespace ThePredictions.Application.Repositories;

public interface IOnboardingSkipRepository
{
    Task AddSkipsAsync(string userId, IEnumerable<string> stepKeys, CancellationToken cancellationToken);
}
