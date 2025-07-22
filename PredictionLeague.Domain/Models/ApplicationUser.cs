using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Identity;

namespace PredictionLeague.Domain.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    public void UpdateDetails(string firstName, string lastName, string? phoneNumber)
    {
        Guard.Against.NullOrWhiteSpace(firstName, nameof(firstName));
        Guard.Against.NullOrWhiteSpace(lastName, nameof(lastName));

        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
    }
}