using System.Data;

namespace PredictionLeague.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}