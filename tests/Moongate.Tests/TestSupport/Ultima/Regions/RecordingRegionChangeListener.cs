using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Regions;

/// <summary>
///     Keeps every region change as "name: previous -> current", with "-" for no region.
/// </summary>
public sealed class RecordingRegionChangeListener : IRegionChangeListener
{
    public List<string> Changes { get; } = [];

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        Changes.Add($"{player.Name}: {previous?.Name ?? "-"} -> {current?.Name ?? "-"}");
    }
}
