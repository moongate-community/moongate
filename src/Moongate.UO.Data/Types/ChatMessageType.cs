namespace Moongate.UO.Data.Types;

/// <summary>
/// UO speech/message wire type (packets 0x03/0xAD/0xAE). Values verified concordant across
/// ModernUO, UOX3 and polserver. Label/Focus and spell-cast overhead text belong to other systems
/// (tooltips, spellcasting) and are intentionally not modeled here, and neither are Guild/Alliance —
/// no guild/alliance system exists yet. See the chat design doc.
/// </summary>
[Flags]
public enum ChatMessageType : byte
{
    Regular = 0x00,
    System = 0x01,
    Emote = 0x02,
    Whisper = 0x08,
    Yell = 0x09,
    Command = 0x0F,

    /// <summary>
    /// Flag composed onto a base type for classic-client "encoded" speech (12-bit-packed keyword
    /// triggers for NPC menus). Never parsed — see <c>UnicodeSpeechPacket</c>.
    /// </summary>
    Encoded = 0xC0
}
