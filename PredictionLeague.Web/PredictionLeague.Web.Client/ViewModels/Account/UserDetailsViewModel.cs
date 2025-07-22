using PredictionLeague.Contracts.Account;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Web.Client.ViewModels.Account;

public class UserDetailsViewModel
{
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    public string Email { get; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public UserDetailsViewModel(UserDetails dto)
    {
        FirstName = dto.FirstName;
        LastName = dto.LastName;
        Email = dto.Email;
        PhoneNumber = dto.PhoneNumber;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public UserDetailsViewModel() { }
}