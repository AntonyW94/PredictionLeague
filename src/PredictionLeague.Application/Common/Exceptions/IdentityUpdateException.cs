namespace PredictionLeague.Application.Common.Exceptions;

public class IdentityUpdateException : Exception
{
    public IEnumerable<string> Errors { get; }

    public IdentityUpdateException(IEnumerable<string> errors) : base("One or more Identity errors occurred.")
    {
        Errors = errors;
    }
}