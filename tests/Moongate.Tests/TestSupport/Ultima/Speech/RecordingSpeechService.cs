using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

/// <summary>
///     Records who said what, who was told what and which sounds were played, and reports each as sent to one player.
/// </summary>
public sealed class RecordingSpeechService : ISpeechService
{
    public List<(MobileEntity Speaker, string Text)> Said { get; } = [];

    public List<(MobileEntity Source, int Sound)> Sounds { get; } = [];

    public List<(MapType Map, Point3D Location, int Sound)> PlacedSounds { get; } = [];

    public List<(MobileEntity Player, string Text)> Told { get; } = [];

    public int Say(MobileEntity speaker, string text)
    {
        Said.Add((speaker, text));

        return 1;
    }

    public int PlaySound(MobileEntity source, int sound)
    {
        Sounds.Add((source, sound));

        return 1;
    }

    public int PlaySound(MapType map, Point3D location, int sound)
    {
        PlacedSounds.Add((map, location, sound));

        return 1;
    }

    public List<(MobileEntity Player, int Cliloc, string Arguments)> ToldClilocs { get; } = [];

    public bool TellCliloc(MobileEntity player, int cliloc, string arguments = "")
    {
        ToldClilocs.Add((player, cliloc, arguments));

        return true;
    }

    public bool Tell(MobileEntity player, string text)
    {
        Told.Add((player, text));

        return true;
    }
}
