using PredictionLeague.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PredictionLeague.Core.Services
{
    public interface IPredictionService
    {
        Task SubmitPredictionsAsync(string userId, int gameWeekId, IEnumerable<UserPrediction> predictions);
        Task CalculatePointsForMatchAsync(int matchId);
    }
}
