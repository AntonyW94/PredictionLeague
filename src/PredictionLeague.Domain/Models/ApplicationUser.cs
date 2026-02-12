using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Identity;

namespace PredictionLeague.Domain.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
  
    public static ApplicationUser Create(string firstName, string lastName, string email)
    {
        Validate(firstName, lastName);
        Guard.Against.NullOrWhiteSpace(email);

        return new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email
        };
    }

    public void UpdateDetails(string firstName, string lastName, string? phoneNumber)
    {
        Validate(firstName, lastName);

        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
    }
    
    private static void Validate(string firstName, string lastName)
    {
        Guard.Against.NullOrWhiteSpace(firstName);
        Guard.Against.NullOrWhiteSpace(lastName);
    }
}