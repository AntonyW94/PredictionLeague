using System.Data;

namespace PredictionLeague.Application.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}