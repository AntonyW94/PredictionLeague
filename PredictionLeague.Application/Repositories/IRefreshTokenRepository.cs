using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken token);
    Task RevokeAsync(string token);
}