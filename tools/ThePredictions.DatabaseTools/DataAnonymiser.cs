using System.Dynamic;

namespace ThePredictions.DatabaseTools;

public class DataAnonymiser
{
    public const string PreservedEmail = "antony.willson@hotmail.com";

    private static readonly Random Random = new();

    public IEnumerable<dynamic> AnonymiseUsers(IEnumerable<dynamic> users)
    {
        var anonymised = new List<dynamic>();
        var counter = 1;

        foreach (var user in users)
        {
            var dict = (IDictionary<string, object?>)user;
            var email = dict["Email"]?.ToString();

            if (string.Equals(email, PreservedEmail, StringComparison.OrdinalIgnoreCase))
            {
                anonymised.Add(user);
                continue;
            }

            var result = new ExpandoObject() as IDictionary<string, object?>;

            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }

            result["Email"] = $"user{counter}@testmail.com";
            result["NormalizedEmail"] = $"USER{counter}@TESTMAIL.COM";
            result["UserName"] = $"user{counter}@testmail.com";
            result["NormalizedUserName"] = $"USER{counter}@TESTMAIL.COM";
            result["FirstName"] = $"TestUser{counter}";
            result["LastName"] = "Player";
            result["PasswordHash"] = "INVALIDATED";
            result["SecurityStamp"] = Guid.NewGuid().ToString();
            result["PhoneNumber"] = null;
            result["PhoneNumberConfirmed"] = false;
            result["TwoFactorEnabled"] = false;
            result["LockoutEnd"] = null;
            result["AccessFailedCount"] = 0;

            anonymised.Add(result);
            counter++;
        }

        Console.WriteLine($"[INFO] Anonymised {counter - 1} users (1 preserved)");
        return anonymised;
    }

    public IEnumerable<dynamic> AnonymiseLeagues(IEnumerable<dynamic> leagues)
    {
        var anonymised = new List<dynamic>();
        var counter = 1;

        foreach (var league in leagues)
        {
            var dict = (IDictionary<string, object?>)league;
            var result = new ExpandoObject() as IDictionary<string, object?>;

            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }

            result["Name"] = $"League {counter}";
            result["EntryCode"] = GenerateRandomEntryCode();

            anonymised.Add(result);
            counter++;
        }

        Console.WriteLine($"[INFO] Anonymised {counter - 1} leagues");
        return anonymised;
    }

    private static string GenerateRandomEntryCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var code = new char[6];

        for (var i = 0; i < code.Length; i++)
        {
            code[i] = chars[Random.Next(chars.Length)];
        }

        return new string(code);
    }
}
