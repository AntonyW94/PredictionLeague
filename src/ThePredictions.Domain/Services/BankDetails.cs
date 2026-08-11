namespace ThePredictions.Domain.Services;

/// <summary>
/// Whether a set of bank details can actually be paid to.
/// </summary>
/// <remarks>
/// All three parts or none. A half-filled account is no use to whoever is trying to send money, so it counts as not set
/// up rather than as partly set up - and showing two of the three would invite somebody to guess the rest.
///
/// The same rule for the two directions money moves: a league's account, which its members pay into, and a winner's
/// account, which the administrator pays out to. Both handlers had their own copy.
/// </remarks>
public static class BankDetails
{
    public static bool AreComplete(string? accountName, string? sortCode, string? accountNumber)
    {
        if (accountName is null)
            return false;

        if (sortCode is null)
            return false;

        return accountNumber is not null;
    }
}
