namespace PredictionLeague.Domain.Services;

public interface IEntryCodeUniquenessChecker
{
    Task<bool> IsCodeUnique(string entryCode, CancellationToken cancellationToken);
}