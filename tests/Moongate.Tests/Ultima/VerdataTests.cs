using Moongate.Tests.Support;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class VerdataTests
{
    [Fact]
    public void Initialize_SyntheticVerdata_PopulatesEntry5DPatches()
    {
        byte[] verdata = UltimaFixtures.BuildVerdata(
            (File: 4, Index: 10, Lookup: 100, Length: 88, Extra: 7),
            (File: 30, Index: 3, Lookup: 200, Length: 26, Extra: 0));

        string dir = UltimaFixtures.CreateClientDirectory(("verdata.mul", verdata));

        try
        {
            Files.SetDirectory(dir);
            Verdata.Initialize();

            Assert.Equal(2, Verdata.Patches.Length);

            Assert.Equal(4, Verdata.Patches[0].File);
            Assert.Equal(10, Verdata.Patches[0].Index);
            Assert.Equal(100, Verdata.Patches[0].Lookup);
            Assert.Equal(88, Verdata.Patches[0].Length);
            Assert.Equal(7, Verdata.Patches[0].Extra);

            Assert.Equal(30, Verdata.Patches[1].File);
            Assert.Equal(3, Verdata.Patches[1].Index);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Initialize_MissingVerdata_YieldsEmptyPatches()
    {
        string dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", [1]));

        try
        {
            Files.SetDirectory(dir);
            Verdata.Initialize();

            Assert.Empty(Verdata.Patches);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
