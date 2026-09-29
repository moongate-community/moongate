using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.TestSupport.Localization;

namespace Moongate.Tests.Server.Core.Extensions;

public sealed class LocalizationExtensionsTests
{
    [Fact]
    public void Text_AKnownMessage_IsTheTranslationWithItsValues()
    {
        var localization = TestLocalization.With((30010, "Comando sconosciuto: {0}"));

        Assert.Equal("Comando sconosciuto: foo", localization.Text(30010, "Unknown command: {0}", "foo"));
    }

    [Fact]
    public void Text_AMissingMessage_IsTheEnglishWithItsValues()
    {
        var localization = TestLocalization.With();

        Assert.Equal("Unknown command: foo", localization.Text(30010, "Unknown command: {0}", "foo"));
    }

    [Fact]
    public void Text_NoLocalization_IsTheEnglish()
    {
        ILocalizationService? localization = null;

        Assert.Equal("Unknown command: foo", localization.Text(30010, "Unknown command: {0}", "foo"));
    }
}
