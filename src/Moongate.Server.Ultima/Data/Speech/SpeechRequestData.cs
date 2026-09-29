using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Data.Speech;

public sealed record SpeechRequestData(SpeechType Type, Hue Hue, SpeechFontType Font, string Language, string Text);
