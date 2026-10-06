namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     What the living hear of a ghost: every word is "oOo".
/// </summary>
internal static class GhostSpeech
{
    private const string Whisper = "oOo";

    /// <summary>
    ///     Gets the text with each word replaced by "oOo", the spaces kept.
    /// </summary>
    public static string Garble(string text)
    {
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(_ => Whisper));
    }
}
