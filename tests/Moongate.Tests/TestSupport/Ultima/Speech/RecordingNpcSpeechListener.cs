using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

/// <summary>
///     Records what the NPCs were told.
/// </summary>
public sealed class RecordingNpcSpeechListener : INpcSpeechListener
{
    public List<(MobileEntity Speaker, string Text)> Heard { get; } = [];

    public List<SpeechType> Types { get; } = [];

    public List<IReadOnlyList<int>> Keywords { get; } = [];

    void INpcSpeechListener.Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords, SpeechType type)
    {
        Heard.Add((speaker, text));
        Types.Add(type);
        Keywords.Add(keywords ?? []);
    }
}
