namespace PredictionLeague.Domain.Common.Exceptions;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string name, object key) : base($"{name} (ID: {key}) was not found.")
    {
    }
}