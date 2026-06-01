using FluentValidation;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Validators.Leagues;

/// <summary>
/// Shared validation for optional peer-to-peer entry-fee bank details on league requests.
/// Bank details are all-or-nothing: leave them blank for manual payment, or provide the
/// account name, sort code and account number together.
/// </summary>
public static class BankDetailValidationExtensions
{
    public static void AddBankDetailRules<T>(this AbstractValidator<T> validator) where T : IHasBankDetails
    {
        validator.RuleFor(x => x.BankAccountName)
            .MaximumLength(100).WithMessage("The account name must be 100 characters or fewer.");

        validator.RuleFor(x => x.BankSortCode)
            .Matches(@"^\d{2}-?\d{2}-?\d{2}$").WithMessage("Enter a valid sort code, for example 12-34-56.")
            .When(x => !string.IsNullOrWhiteSpace(x.BankSortCode));

        validator.RuleFor(x => x.BankAccountNumber)
            .Matches(@"^\d{8}$").WithMessage("Enter a valid 8-digit account number.")
            .When(x => !string.IsNullOrWhiteSpace(x.BankAccountNumber));

        validator.RuleFor(x => x.PaymentReferenceTemplate)
            .MaximumLength(100).WithMessage("The payment reference must be 100 characters or fewer.");

        // All-or-nothing: if any bank field is filled in, the three core fields are all required.
        validator.RuleFor(x => x.BankAccountName)
            .NotEmpty().WithMessage("Enter the account name, or leave all bank details blank.")
            .When(x => HasAnyBankField(x));

        validator.RuleFor(x => x.BankSortCode)
            .NotEmpty().WithMessage("Enter the sort code, or leave all bank details blank.")
            .When(x => HasAnyBankField(x));

        validator.RuleFor(x => x.BankAccountNumber)
            .NotEmpty().WithMessage("Enter the account number, or leave all bank details blank.")
            .When(x => HasAnyBankField(x));
    }

    private static bool HasAnyBankField(IHasBankDetails x) =>
        !string.IsNullOrWhiteSpace(x.BankAccountName)
        || !string.IsNullOrWhiteSpace(x.BankSortCode)
        || !string.IsNullOrWhiteSpace(x.BankAccountNumber);
}
