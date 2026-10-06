using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Names the places as UOX3 and Sphere do. A place is a named region that is not a mere part of the one around
///     it: a field or a shop inside a town has the town's kind and the town's guards, so it says nothing, while a
///     guarded town on an island without guards is a place of its own. Walking in names the places entered, the outer
///     first; walking out names the ones left. The guards bear the name of the nearest guarded place, or none: then
///     the client's own texts are used. The same name, such as the same town on another map, is the same place.
/// </summary>
public sealed class RegionAnnouncer : IRegionAnnouncer
{
    public const int EnteredMessage = 30134;
    public const int LeftMessage = 30135;
    public const int GuardedMessage = 30136;
    public const int UnguardedMessage = 30137;

    // The client's own texts, for guards that bear no name.
    public const int GuardedCliloc = 500112;
    public const int UnguardedCliloc = 500113;

    // What is entered reads green, what is left reads red.
    public const int EnterHue = 0x3F;
    public const int LeaveHue = 0x22;

    private readonly ISpeechService _speech;
    private readonly IMoongateEventBus? _events;
    private readonly IGameLoopService? _loop;
    private readonly ILocalizationService? _localization;
    private readonly Lazy<Dictionary<(MapType Map, string Name), RegionContent>> _byName;

    // The players whose login is complete, and where the others stood when they entered the world. Game loop only.
    private readonly HashSet<Serial> _ready = [];
    private readonly Dictionary<Serial, RegionContent?> _waiting = [];

    private IDisposable? _logins;

    public RegionAnnouncer(
        IDataLoaderService data,
        ISpeechService speech,
        IMoongateEventBus? events = null,
        IGameLoopService? loop = null,
        ILocalizationService? localization = null
    )
    {
        _speech = speech;
        _events = events;
        _loop = loop;
        _localization = localization;
        _byName = new(() => data.GetEntities<RegionContent>()
            .Where(region => region.Name is not null)
            .GroupBy(region => (region.Map, region.Name!))
            .ToDictionary(group => group.Key, group => group.First())
        );
    }

    public Task StartAsync()
    {
        if (_events is not null && _loop is not null)
        {
            // The event comes from the login handler's thread; the players are followed on the game loop only.
            _logins = _events.Subscribe<CharacterEnteredWorldEvent>(async (evt, cancellationToken) =>
                {
                    var work = new LoopActionWorkItem(() => LoggedIn(evt.Character));
                    await _loop.PostAsync(work, cancellationToken);
                    await work.Completion;
                }
            );
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _logins?.Dispose();
        _logins = null;

        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        // A client that is not in game yet would not show the text: it is told once its login is complete.
        if (!_ready.Contains(player.Id))
        {
            _waiting[player.Id] = current;

            return;
        }

        Announce(player, previous, current);
    }

    /// <summary>
    ///     The player's login is complete: it is told where it stands.
    /// </summary>
    public void LoggedIn(MobileEntity player)
    {
        if (!_waiting.Remove(player.Id, out var region))
        {
            return;
        }

        _ready.Add(player.Id);
        Announce(player, null, region);
    }

    public void Left(Serial player)
    {
        _ready.Remove(player);
        _waiting.Remove(player);
    }

    private void Announce(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        var from = Places(previous);
        var to = Places(current);
        var wasGuarded = previous?.Guarded == true;
        var isGuarded = current?.Guarded == true;
        var oldGuards = wasGuarded ? from.FirstOrDefault(place => place.Guarded)?.Name : null;
        var newGuards = isGuarded ? to.FirstOrDefault(place => place.Guarded)?.Name : null;

        // Out first, the inner place before the one around it; then in, the outer first.
        if (wasGuarded && (!isGuarded || oldGuards != newGuards))
        {
            Tell(
                player,
                UnguardedMessage,
                "You have left the protection of the guards of {0}.",
                oldGuards,
                UnguardedCliloc,
                LeaveHue
            );
        }

        foreach (var place in from.Where(place => to.All(other => other.Name != place.Name)))
        {
            _speech.Tell(player, _localization.Text(LeftMessage, "You have left {0}.", place.Name), LeaveHue);
        }

        foreach (var place in to.Where(place => from.All(other => other.Name != place.Name)).Reverse())
        {
            _speech.Tell(player, _localization.Text(EnteredMessage, "You have entered {0}.", place.Name), EnterHue);
        }

        if (isGuarded && (!wasGuarded || oldGuards != newGuards))
        {
            Tell(
                player,
                GuardedMessage,
                "You are now under the protection of the guards of {0}.",
                newGuards,
                GuardedCliloc,
                EnterHue
            );
        }
    }

    private void Tell(MobileEntity player, int message, string english, string? name, int cliloc, int hue)
    {
        if (name is null)
        {
            _speech.TellCliloc(player, cliloc, "", hue);

            return;
        }

        _speech.Tell(player, _localization.Text(message, english, name), hue);
    }

    // The places around a region, the innermost first. The loader refused loops and lost parents.
    private List<RegionContent> Places(RegionContent? region)
    {
        var places = new List<RegionContent>();
        var steps = 0;

        while (region is not null && steps++ <= _byName.Value.Count)
        {
            var around = region.Parent is { } parent && _byName.Value.TryGetValue((region.Map, parent), out var found)
                ? found
                : null;

            if (IsPlace(region, around))
            {
                places.Add(region);
            }

            region = around;
        }

        return places;
    }

    // A spot that only marks guards, such as the moongates, is not a place; nor is a part of the place around it.
    private static bool IsPlace(RegionContent region, RegionContent? around)
    {
        return region.Name is not null &&
               region.Type != RegionType.Guarded &&
               (around is null || around.Guarded != region.Guarded || around.Type != region.Type);
    }
}
