using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Data.Speech;

public sealed record SpeechRequestData(SpeechType Type, Hue Hue, SpeechFontType Font, string Language, string Text)
{
    /// <summary>
    ///     Gets the speech keywords the client found in the text (speech.mul ids, such as 0x0002 for "bank"), whatever
    ///     the language of the client; empty when it sent none.
    /// </summary>
    public IReadOnlyList<int> Keywords { get; init; } = [];
}
