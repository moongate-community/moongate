using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

/// <summary>
///     Records who said what and which sounds were played, and reports each as sent to one player.
/// </summary>
public sealed class RecordingSpeechService : ISpeechService
{
    public List<(MobileEntity Speaker, string Text)> Said { get; } = [];

    public List<(MobileEntity Source, int Sound)> Sounds { get; } = [];

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
}
