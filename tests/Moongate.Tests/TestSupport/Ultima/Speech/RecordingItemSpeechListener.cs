using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

/// <summary>
///     Records what the items around a player were told the player said.
/// </summary>
public sealed class RecordingItemSpeechListener : IItemSpeechListener
{
    public List<(MobileEntity Speaker, string Text)> Heard { get; } = [];

    public List<SpeechType> Types { get; } = [];

    void IItemSpeechListener.Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords, SpeechType type)
    {
        Heard.Add((speaker, text));
        Types.Add(type);
    }
}
