using Moongate.Network.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.Stress.Internal;

internal static class UoPacketWriter
{
    public static byte[] AccountLogin(string account, string password)
    {
        var writer = new SpanWriter(62, true);
        writer.Write((byte)0x80);
        writer.WriteAscii(account, 30);
        writer.WriteAscii(password, 30);
        writer.Write((byte)0);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] CharacterCreation(string characterName)
    {
        var writer = new SpanWriter(106, true);

        writer.Write((byte)0xF8);
        writer.Write(unchecked((int)0xEDEDEDED));
        writer.Write(unchecked((int)0xFFFFFFFF));
        writer.Write((byte)0x00);
        writer.WriteAscii(characterName, 30);
        writer.Write((ushort)0);
        writer.Write((uint)ClientFlags.Trammel);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)2);
        writer.Clear(15);
        writer.Write((byte)0); // male human
        writer.Write((byte)60);
        writer.Write((byte)50);
        writer.Write((byte)40);
        writer.Write((byte)0);
        writer.Write((byte)50);
        writer.Write((byte)1);
        writer.Write((byte)50);
        writer.Write((byte)2);
        writer.Write((byte)50);
        writer.Write((byte)3);
        writer.Write((byte)50);
        writer.Write((short)0x0455);
        writer.Write((short)0x0203);
        writer.Write((short)0x0304);
        writer.Write((short)0x0000);
        writer.Write((short)0x0000);
        writer.Write((short)3); // city index
        writer.Write((ushort)0);
        writer.Write((short)1); // slot
        writer.Write(0);
        writer.Write((short)0x0888);
        writer.Write((short)0x0999);

        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] GameLogin(uint sessionKey, string account, string password)
    {
        var writer = new SpanWriter(65, true);
        writer.Write((byte)0x91);
        writer.Write(sessionKey);
        writer.WriteAscii(account, 30);
        writer.WriteAscii(password, 30);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] LoginCharacter(string characterName)
    {
        var writer = new SpanWriter(73, true);
        writer.Write((byte)0x5D);
        writer.Write(unchecked((int)0xEDEDEDED));
        writer.WriteAscii(characterName, 30);
        writer.Write((ushort)0);
        writer.Write((uint)ClientFlags.Trammel);
        writer.Write(0);
        writer.Write(0);
        writer.Clear(16);
        writer.Write(0);
        writer.Write((uint)0);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] LoginSeed(uint seed)
    {
        var writer = new SpanWriter(21, true);
        writer.Write((byte)0xEF);
        writer.Write((int)seed);
        writer.Write(7);
        writer.Write(0);
        writer.Write(16);
        writer.Write(0);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] Move(DirectionType direction, byte sequence)
    {
        var writer = new SpanWriter(7, true);
        writer.Write((byte)0x02);
        writer.Write((byte)direction);
        writer.Write(sequence);
        writer.Write((uint)0);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] SeedOnly(uint seed)
    {
        var writer = new SpanWriter(4, true);
        writer.Write(seed);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    public static byte[] ServerSelect(short index)
    {
        var writer = new SpanWriter(3, true);
        writer.Write((byte)0xA0);
        writer.WriteLE(index);
        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }
}
