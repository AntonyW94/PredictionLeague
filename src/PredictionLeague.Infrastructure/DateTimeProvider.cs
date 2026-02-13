using PredictionLeague.Domain.Common;

namespace PredictionLeague.Infrastructure;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
