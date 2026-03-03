using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Speech;

/// <summary>
/// Outgoing chat command packet (0xB2).
/// </summary>
public class ChatCommandPacket : BaseGameNetworkPacket
{
    public ChatCommandType Command { get; set; }

    public string Language { get; set; } = "ENU";

    public string Param1 { get; set; }

    public string Param2 { get; set; }

    public ChatCommandPacket()
        : base(0xB2) { }

    public ChatCommandPacket(ChatCommandType command, string param1 = "", string param2 = "", string language = "ENU")
        : this()
    {
        Command = command;
        Param1 = param1 ?? string.Empty;
        Param2 = param2 ?? string.Empty;
        Language = string.IsNullOrWhiteSpace(language) ? "ENU" : language;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((ushort)Command);
        writer.WriteAscii(Language, 4);
        writer.WriteBigUniNull(Param1);
        writer.WriteBigUniNull(Param2);
        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => false;
}
