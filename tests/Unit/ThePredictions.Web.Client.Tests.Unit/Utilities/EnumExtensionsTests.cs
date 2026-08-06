using System.ComponentModel;
using FluentAssertions;
using ThePredictions.Web.Client.Utilities;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Utilities;

public class EnumExtensionsTests
{
    private enum SampleStatus
    {
        [Description("Not started yet")]
        NotStarted,

        [Description("")]
        BlankDescription,

        NoDescriptionAttribute
    }

    [Fact]
    public void GetDescription_ShouldReturnTheDescription_WhenTheMemberHasOne()
    {
        SampleStatus.NotStarted.GetDescription().Should().Be("Not started yet");
    }

    [Fact]
    public void GetDescription_ShouldReturnTheMemberName_WhenThereIsNoDescriptionAttribute()
    {
        SampleStatus.NoDescriptionAttribute.GetDescription().Should().Be("NoDescriptionAttribute");
    }

    [Fact]
    public void GetDescription_ShouldReturnTheEmptyDescription_WhenTheAttributeIsBlank()
    {
        // An explicitly blank Description is a deliberate choice, so it wins over the member name.
        SampleStatus.BlankDescription.GetDescription().Should().BeEmpty();
    }

    [Fact]
    public void GetDescription_ShouldFallBackToTheNumericValue_ForAnUndefinedMember()
    {
        // Casting an unmapped number yields no member to read an attribute from, so the value's
        // own ToString is all that is left.
        ((SampleStatus)99).GetDescription().Should().Be("99");
    }
}
