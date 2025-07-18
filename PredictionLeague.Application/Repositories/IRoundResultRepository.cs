using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRoundResultRepository
{
    Task UpsertAsync(RoundResult result);
}