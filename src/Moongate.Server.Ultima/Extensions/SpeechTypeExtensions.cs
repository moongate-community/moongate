using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     What a <see cref="SpeechType" /> says of who hears it.
/// </summary>
public static class SpeechTypeExtensions
{
    extension(SpeechType type)
    {
        /// <summary>
        ///     Gets whether a player may speak this way: aloud, as an emote, in a whisper or in a yell.
        /// </summary>
        public bool IsSpoken => type is SpeechType.Regular or SpeechType.Emote or SpeechType.Whisper or SpeechType.Yell;

        /// <summary>
        ///     Gets how many cells away it is heard, as ModernUO: 1 for a whisper, 18 for a yell, 15 for every other.
        /// </summary>
        public int Range => type switch
        {
            SpeechType.Whisper => 1,
            SpeechType.Yell    => 18,
            _                  => 15
        };
    }
}
