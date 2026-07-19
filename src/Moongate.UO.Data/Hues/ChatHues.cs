namespace Moongate.UO.Data.Hues;

/// <summary>
/// Well-known text hues for chat messages. <see cref="Default" /> is the OSI default speech-text
/// hue — verified against ModernUO's <c>Mobile.SendMessage</c>/<c>OutgoingMessagePackets</c> and
/// moongatev2's <c>SpeechHues.Default</c>, which agree. <see cref="Broadcast" /> matches ModernUO's
/// <c>World.Broadcast</c>/<c>BroadcastStaff</c>.
/// </summary>
public static class ChatHues
{
    public static readonly Hue Default = new(0x3B2);
    public static readonly Hue Broadcast = new(0x35);
}
