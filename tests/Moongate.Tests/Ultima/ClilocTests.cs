using Moongate.Tests.Support;
using Moongate.Ultima.Localization;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Ultima;

public class ClilocTests
{
    [Fact]
    public void GetEntry_PreservesNumberFlagAndText()
    {
        var cliloc = UltimaFixtures.BuildCliloc((1234, (byte)CliLocFlagType.Modified, "Custom text"));
        var dir = UltimaFixtures.CreateClientDirectory(("cliloc.enu", cliloc));

        try
        {
            var list = new StringList("enu", Path.Combine(dir, "cliloc.enu"), false);
            var entry = list.GetEntry(1234);

            Assert.NotNull(entry);
            Assert.Equal(1234, entry.Number);
            Assert.Equal("Custom text", entry.Text);
            Assert.Equal(CliLocFlagType.Modified, entry.Flag);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetString_KnownEntries_ReturnsText()
    {
        var cliloc = UltimaFixtures.BuildCliloc((3000000, 0, "Hello"), (3000001, 0, "World"));
        var dir = UltimaFixtures.CreateClientDirectory(("cliloc.enu", cliloc));

        try
        {
            var list = new StringList("enu", Path.Combine(dir, "cliloc.enu"), false);

            Assert.Equal("Hello", list.GetString(3000000));
            Assert.Equal("World", list.GetString(3000001));
            Assert.Equal(2, list.Entries.Count);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetString_UnknownNumber_ReturnsNull()
    {
        var cliloc = UltimaFixtures.BuildCliloc((3000000, 0, "Hello"));
        var dir = UltimaFixtures.CreateClientDirectory(("cliloc.enu", cliloc));

        try
        {
            var list = new StringList("enu", Path.Combine(dir, "cliloc.enu"), false);

            Assert.Null(list.GetString(999999));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
