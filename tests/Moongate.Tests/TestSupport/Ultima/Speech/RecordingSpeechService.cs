using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

/// <summary>
///     Records who said what, and reports it as sent to one player.
/// </summary>
public sealed class RecordingSpeechService : ISpeechService
{
    public List<(MobileEntity Speaker, string Text)> Said { get; } = [];

    public int Say(MobileEntity speaker, string text)
    {
        Said.Add((speaker, text));

        return 1;
    }
}
