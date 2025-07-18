using Microsoft.AspNetCore.Identity;

namespace PredictionLeague.Application.Common.Exceptions;

public class IdentityUpdateException : Exception
{
    public IEnumerable<IdentityError> Errors { get; }

    public IdentityUpdateException(IEnumerable<IdentityError> errors) : base("One or more Identity errors occurred.")
    {
        Errors = errors;
    }
}