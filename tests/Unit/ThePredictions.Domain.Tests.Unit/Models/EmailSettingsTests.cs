using FluentAssertions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class EmailSettingsTests
{
    [Fact]
    public void CreateDefault_ShouldUseDefaultConstant()
    {
        var settings = EmailSettings.CreateDefault();

        settings.EmailsEnabled.Should().Be(EmailSettings.DefaultEmailsEnabled);
        settings.EmailsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabase()
    {
        var settings = new EmailSettings(1, false);

        settings.Id.Should().Be(1);
        settings.EmailsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_ShouldReplaceValue(bool emailsEnabled)
    {
        var settings = new EmailSettings(1, !emailsEnabled);

        settings.Update(emailsEnabled);

        settings.EmailsEnabled.Should().Be(emailsEnabled);
    }
}
