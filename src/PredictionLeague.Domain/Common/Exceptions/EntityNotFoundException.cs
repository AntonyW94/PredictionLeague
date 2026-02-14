namespace PredictionLeague.Domain.Common.Exceptions;

public class EntityNotFoundException(string name, object key) : Exception($"{name} (ID: {key}) was not found.");