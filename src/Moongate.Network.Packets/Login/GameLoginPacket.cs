using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Internal.Login;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Login;

[PacketHandler(0x91, PacketSizing.Fixed, Length = 65)]
public sealed class GameLoginPacket : BaseFixedPacket<GameLoginPacket>, IIncomingPacket<GameLoginPacket>
{
    public uint AuthKey { get; }
    public string Account { get; }
    public string Password { get; }

    public GameLoginPacket(uint authKey, string account, string password)
    {
        PacketValidation.ValidateFixedAscii(account, LoginProtocolConstants.CredentialLength, nameof(account));
        PacketValidation.ValidateFixedAscii(password, LoginProtocolConstants.CredentialLength, nameof(password));
        AuthKey = authKey;
        Account = account;
        Password = password;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out GameLoginPacket? packet)
    {
        packet = null;
        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt32BigEndian(out var authKey)
            || !reader.TryReadFixedAscii(LoginProtocolConstants.CredentialLength, out var account)
            || !reader.TryReadFixedAscii(LoginProtocolConstants.CredentialLength, out var password))
        {
            return false;
        }

        packet = new GameLoginPacket(authKey, account!, password!);
        return true;
    }
}
