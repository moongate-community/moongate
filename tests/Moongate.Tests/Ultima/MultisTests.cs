using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Multi;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class MultisTests
{
    [Fact]
    public void GetComponents_MultiFixture_ParsesTiles()
    {
        var (idx, mul) = UltimaFixtures.BuildMulti(0, (0x10, 0, 0, 0), (0x11, 1, 0, 5));
        var dir = UltimaFixtures.CreateClientDirectory(("Multi.idx", idx), ("Multi.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Multis.Reload();

            var mcl = Multis.GetComponents(0);

            Assert.Equal(2, mcl.SortedTiles.Length);
            Assert.Contains(mcl.SortedTiles, t => t.ItemId == 0x10);
            Assert.Contains(mcl.SortedTiles, t => t.ItemId == 0x11);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
