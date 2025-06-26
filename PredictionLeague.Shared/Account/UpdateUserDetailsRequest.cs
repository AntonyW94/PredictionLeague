using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Account
{
    public class UpdateUserDetailsRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string LastName { get; set; } = string.Empty;

        [RegularExpression(@"^07\d{9}$", ErrorMessage = "Please enter a valid 11-digit UK mobile number starting with 07.")]
        public string? PhoneNumber { get; set; }
    }
}