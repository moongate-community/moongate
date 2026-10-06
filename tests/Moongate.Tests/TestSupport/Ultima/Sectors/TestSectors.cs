using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Sectors;

/// <summary>
///     Builds a <see cref="SectorService" /> over Trammel and Felucca at 7168×4096, with the default view range unless a
///     world config is given, and a recording NPC tick unless one is given.
/// </summary>
public static class TestSectors
{
    public static SectorService Create(WorldConfig? world = null, INpcTickService? ticks = null)
    {
        var loaders = new StubDataLoaderService().With(
            new MapContent { Map = MapType.Trammel, Size = new Point2D(7168, 4096), Name = "Trammel" },
            new MapContent { Map = MapType.Felucca, Size = new Point2D(7168, 4096), Name = "Felucca" },
            new MapContent { Map = MapType.Ilshenar, Size = new Point2D(2304, 1600), Name = "Ilshenar" },
            new MapContent { Map = MapType.Malas, Size = new Point2D(2560, 2048), Name = "Malas" }
        );

        return new(loaders, world ?? new WorldConfig(), ticks ?? new RecordingNpcTickService());
    }
}
