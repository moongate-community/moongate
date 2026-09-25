using Moongate.Ultima.Types;

namespace Moongate.Tests.Ultima.Types;

public sealed class MusicTypeTests
{
    [Fact]
    public void Values_AreTheClientIdsFrom0To104InOrder()
    {
        var tracks = Enum.GetValues<MusicType>().Where(track => track != MusicType.NoMusic).Select(track => (int)track);

        Assert.Equal(Enumerable.Range(0, 105), tracks.Order());
    }

    [Theory,
     InlineData(MusicType.OldUlt01, 0),
     InlineData(MusicType.Britain1, 9),
     InlineData(MusicType.TheVesperMist, 102),
     InlineData(MusicType.Townlife, 103),
     InlineData(MusicType.DungeonOutpost, 104),
     InlineData(MusicType.NoMusic, 0x1FFF)]
    public void KnownTracks_HaveTheirClientIds(MusicType track, int id)
    {
        Assert.Equal(id, (int)track);
    }
}
