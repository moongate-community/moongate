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

    public List<(MobileEntity Speaker, int Cliloc, string Arguments)> SaidClilocs { get; } = [];

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

    /// <summary>
    ///     The colour asked for each cliloc told, in order; null for the usual one.
    /// </summary>
    public List<int?> ToldClilocHues { get; } = [];

    /// <summary>
    ///     The colour asked for each text told, in order; null for the usual one.
    /// </summary>
    public List<int?> ToldHues { get; } = [];

    public bool TellCliloc(MobileEntity player, int cliloc, string arguments = "", int? hue = null)
    {
        ToldClilocs.Add((player, cliloc, arguments));
        ToldClilocHues.Add(hue);

        return true;
    }

    public int SayCliloc(MobileEntity speaker, int cliloc, string arguments = "")
    {
        SaidClilocs.Add((speaker, cliloc, arguments));

        return 1;
    }

    public bool Tell(MobileEntity player, string text, int? hue = null)
    {
        Told.Add((player, text));
        ToldHues.Add(hue);

        return true;
    }
}
