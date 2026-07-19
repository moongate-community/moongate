using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Chat;

/// <summary>
/// Builds outgoing <see cref="UnicodeSpeechMessagePacket" />s, applying the default-hue fallback so
/// <see cref="ChatService" /> and <c>ChatModule</c> never duplicate that logic.
/// </summary>
public static class ChatMessageFactory
{
    private const string SystemName = "System";

    public static UnicodeSpeechMessagePacket CreateFromMobile(
        Serial speaker,
        string speakerName,
        int body,
        ChatMessageType type,
        Hue hue,
        string text
    )
        => new(speaker, (ushort)body, type, ResolveHue(hue), speakerName, text);

    public static UnicodeSpeechMessagePacket CreateSystem(string text, Hue? hue = null)
        => new(Serial.Zero, 0, ChatMessageType.System, hue ?? ChatHues.Broadcast, SystemName, text);

    private static Hue ResolveHue(Hue hue)
        => hue.IsDefault ? ChatHues.Default : hue;
}
