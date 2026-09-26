using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void Get_FillsInValuesAndUnescapesBraces()
    {
        var service = CreateService();

        Assert.Equal("Sali a bordo della barca.", service.Get(0));
        Assert.Equal("Bob è stato ucciso da un drago!", service.Get(1, "Bob", "un drago"));
        Assert.Equal("{tag} 0x1f", service.Get(2, 31));
    }

    [Fact]
    public void Get_UnknownIdOrMissingValue_Throws()
    {
        var service = CreateService();

        Assert.Throws<KeyNotFoundException>(() => service.Get(99));
        Assert.Throws<FormatException>(() => service.Get(1, "Bob"));
    }

    [Fact]
    public void TryGetText_ReturnsTheTextAsWritten()
    {
        var service = CreateService();

        Assert.True(service.TryGetText(2, out var text));
        Assert.Equal("{{tag}} 0x{0:x}", text);
        Assert.False(service.TryGetText(99, out _));
    }

    [Fact]
    public void Language_IsTheConfiguredCodeInLowerCase()
    {
        Assert.Equal("ita", CreateService().Language);
    }

    private static LocalizationService CreateService()
    {
        var dataLoaderService = new StubDataLoaderService().With(
            new MessageContent { Id = 0, Text = "Sali a bordo della barca." },
            new MessageContent { Id = 1, Text = "{0} è stato ucciso da {1}!" },
            new MessageContent { Id = 2, Text = "{{tag}} 0x{0:x}" }
        );

        return new(new LocalizationConfig { Language = "ITA" }, dataLoaderService);
    }
}
