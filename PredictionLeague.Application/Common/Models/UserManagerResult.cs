namespace PredictionLeague.Application.Common.Models;

public class UserManagerResult
{
    public bool Succeeded { get; }
    public IEnumerable<string> Errors { get; }

    private UserManagerResult(bool succeeded, IEnumerable<string>? errors)
    {
        Succeeded = succeeded;
        Errors = errors ?? new List<string>();
    }

    public static UserManagerResult Success()
    {
        return new UserManagerResult(true, new string[] { });
    }

    public static UserManagerResult Failure(IEnumerable<string> errors)
    {
        return new UserManagerResult(false, errors);
    }
}