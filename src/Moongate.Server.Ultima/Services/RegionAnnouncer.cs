using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Tells a player the place it walks into and out of, and whether guards protect it there, as UOX3 and Sphere do:
///     "You have entered Britain.", "You are now under the protection of the guards of Britain." The place is the
///     outermost region around the player, so a field or a shop inside a town says nothing; a place with the same
///     name, such as the same town on another map, says nothing either.
/// </summary>
public sealed class RegionAnnouncer : IRegionChangeListener
{
    public const int EnteredMessage = 30134;
    public const int LeftMessage = 30135;
    public const int GuardedMessage = 30136;
    public const int UnguardedMessage = 30137;

    // The client's own texts, for a guarded place that has no name.
    public const int GuardedCliloc = 500112;
    public const int UnguardedCliloc = 500113;

    private readonly ISpeechService _speech;
    private readonly ILocalizationService? _localization;
    private readonly Lazy<Dictionary<(MapType Map, string Name), RegionContent>> _byName;

    public RegionAnnouncer(IDataLoaderService data, ISpeechService speech, ILocalizationService? localization = null)
    {
        _speech = speech;
        _localization = localization;
        _byName = new(
            () => data.GetEntities<RegionContent>()
                      .Where(region => region.Name is not null)
                      .GroupBy(region => (region.Map, region.Name!))
                      .ToDictionary(group => group.Key, group => group.First())
        );
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        var from = Place(previous);
        var to = Place(current);
        var sameName = from?.Name == to?.Name;
        var wasGuarded = previous?.Guarded == true;
        var isGuarded = current?.Guarded == true;

        // Out first, then in. The guards are named again only when the place changes or the protection does.
        if (wasGuarded && (!isGuarded || !sameName))
        {
            Tell(player, UnguardedMessage, "You have left the protection of the guards of {0}.", from?.Name, UnguardedCliloc);
        }

        if (!sameName && from?.Name is { } left)
        {
            _speech.Tell(player, _localization.Text(LeftMessage, "You have left {0}.", left));
        }

        if (!sameName && to?.Name is { } entered)
        {
            _speech.Tell(player, _localization.Text(EnteredMessage, "You have entered {0}.", entered));
        }

        if (isGuarded && (!wasGuarded || !sameName))
        {
            Tell(player, GuardedMessage, "You are now under the protection of the guards of {0}.", to?.Name, GuardedCliloc);
        }
    }

    public void Left(Serial player)
    {
    }

    private void Tell(MobileEntity player, int message, string english, string? name, int cliloc)
    {
        if (name is null)
        {
            _speech.TellCliloc(player, cliloc);

            return;
        }

        _speech.Tell(player, _localization.Text(message, english, name));
    }

    // The outermost region around this one: the town a field belongs to. The loader refused loops and lost parents.
    private RegionContent? Place(RegionContent? region)
    {
        var steps = 0;

        while (region?.Parent is { } parent &&
               _byName.Value.TryGetValue((region.Map, parent), out var around) &&
               steps++ < _byName.Value.Count)
        {
            region = around;
        }

        return region;
    }
}
