using Moongate.Server.Core.Data.Config;

namespace Moongate.Tests.Server.Core.Data.Config;

public sealed class LocalizationConfigTests
{
    [Fact]
    public void Language_DefaultsToEnglish()
    {
        var config = new LocalizationConfig();

        config.Validate();
        Assert.Equal("eng", config.Language);
    }

    [Theory, InlineData(""), InlineData("../eng"), InlineData("it a"), InlineData(null)]
    public void Validate_LanguageThatIsNotALetterCode_Throws(string? language)
    {
        var config = new LocalizationConfig { Language = language! };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
