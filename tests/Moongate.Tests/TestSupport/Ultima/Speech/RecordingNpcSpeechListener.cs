using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

/// <summary>
///     Records what the NPCs were told.
/// </summary>
public sealed class RecordingNpcSpeechListener : INpcSpeechListener
{
    public List<(MobileEntity Speaker, string Text)> Heard { get; } = [];

    void INpcSpeechListener.Heard(MobileEntity speaker, string text)
    {
        Heard.Add((speaker, text));
    }
}
