using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Regions;

/// <summary>
///     Keeps every region change as "name: previous -> current", with "-" for no region, running
///     <see cref="OnChange" /> first so a test can look at the state at that moment.
/// </summary>
public sealed class RecordingRegionChangeListener : IRegionChangeListener
{
    public List<string> Changes { get; } = [];

    public Action? OnChange { get; set; }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        OnChange?.Invoke();
        Changes.Add($"{player.Name}: {previous?.Name ?? "-"} -> {current?.Name ?? "-"}");
    }

    public void Left(Serial player)
    {
        Changes.Add($"{player} left");
    }
}
