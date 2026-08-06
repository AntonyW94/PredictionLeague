using FluentValidation;
using FluentValidation.TestHelper;
using ThePredictions.Validators.Common;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Common;

/// <summary>
/// Every validator in the application guards this rule behind a not-empty check, so these exercise
/// the extension directly. It has to hold up on its own: the next caller may not add that guard,
/// and emptiness is a separate rule's job, not this one's.
/// </summary>
public class SafeNameValidationExtensionsTests
{
    private sealed record NameHolder(string Name);

    /// <summary>Deliberately unguarded, unlike the real validators.</summary>
    private sealed class UnguardedValidator : AbstractValidator<NameHolder>
    {
        public UnguardedValidator() => RuleFor(x => x.Name).MustBeASafeName("Test name");
    }

    private readonly UnguardedValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MustBeASafeName_ShouldAcceptAnEmptyName(string? name)
    {
        // Emptiness is NotEmpty's job. If this rejected blanks too, a missing name would produce
        // two errors saying different things.
        _validator.TestValidate(new NameHolder(name!)).ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("Manchester United")]
    [InlineData("AFC Bournemouth (2024-25)")]
    [InlineData("Brighton & Hove Albion")]
    [InlineData("Well, really?")]
    [InlineData("Borussia Mönchengladbach")]
    [InlineData("Ivry-sur-Seine: Group A; B")]
    [InlineData("1. FC Köln")]
    public void MustBeASafeName_ShouldAcceptLettersNumbersAndCommonPunctuation(string name)
    {
        _validator.TestValidate(new NameHolder(name)).ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("<b>Team</b>")]
    [InlineData("Team \"quoted\"")]
    [InlineData("Team 'quoted'")]
    [InlineData("Team `backtick`")]
    [InlineData(@"Team\path")]
    [InlineData("Team/path")]
    public void MustBeASafeName_ShouldRejectCharactersUsedForInjection(string name)
    {
        _validator.TestValidate(new NameHolder(name))
            .ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Test name can only contain letters, numbers, spaces, and common punctuation (- . , ! ? & ( ) : ;).");
    }
}
