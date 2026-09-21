using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Internal.Login;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Incoming.Login;

[PacketHandler(0x80, PacketSizing.Fixed, Length = 62)]
public sealed class AccountLoginPacket : BaseFixedPacket<AccountLoginPacket>, IIncomingPacket<AccountLoginPacket>
{
    public string Account { get; }
    public string Password { get; }
    public byte NextLoginKey { get; }

    public AccountLoginPacket(string account, string password, byte nextLoginKey)
    {
        PacketValidation.ValidateFixedAscii(account, LoginProtocolConstants.CredentialLength, nameof(account));
        PacketValidation.ValidateFixedAscii(password, LoginProtocolConstants.CredentialLength, nameof(password));
        Account = account;
        Password = password;
        NextLoginKey = nextLoginKey;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out AccountLoginPacket? packet)
    {
        packet = null;
        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadFixedAscii(LoginProtocolConstants.CredentialLength, out var account)
            || !reader.TryReadFixedAscii(LoginProtocolConstants.CredentialLength, out var password)
            || !reader.TryReadByte(out var nextLoginKey))
        {
            return false;
        }

        packet = new AccountLoginPacket(account!, password!, nextLoginKey);
        return true;
    }
}
