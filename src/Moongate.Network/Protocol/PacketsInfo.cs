using Moongate.Network.Data;
using Moongate.Network.Types;

namespace Moongate.Network.Protocol;

/// <summary>
/// Static catalog of known protocol 7.x packets: name, direction and size per id.
/// Sizes stay consistent with <see cref="PacketLengths"/> for every input-capable id
/// (locked by a test); output-only ids carry their own size here.
/// </summary>
public static class PacketsInfo
{
    private static readonly PacketInfo?[] _table = BuildTable();

    public static PacketInfo? GetPacket(byte packetId)
    {
        return _table[packetId];
    }

    private static PacketInfo?[] BuildTable()
    {
        var table = new PacketInfo?[256];

        void Add(byte id, string name, PacketDirectionType direction, short size)
        {
            table[id] = new PacketInfo
            {
                Id = id,
                Name = name,
                Direction = direction,
                Size = size
            };
        }

        Add(0x00, "CharacterCreation", PacketDirectionType.Input, 104);
        Add(0x01, "DisconnectNotification", PacketDirectionType.Input, 5);
        Add(0x02, "MoveRequest", PacketDirectionType.Input, 7);
        Add(0x03, "AsciiSpeech", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x05, "AttackRequest", PacketDirectionType.Input, 5);
        Add(0x06, "DoubleClick", PacketDirectionType.Input, 5);
        Add(0x07, "PickUpItem", PacketDirectionType.Input, 7);
        Add(0x08, "DropItem", PacketDirectionType.Input, 15);
        Add(0x09, "SingleClick", PacketDirectionType.Input, 5);
        Add(0x11, "MobileStatus", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x12, "TextCommand", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0x13, "EquipItem", PacketDirectionType.Input, 10);
        Add(0x1B, "LoginConfirm", PacketDirectionType.Output, 37);
        Add(0x20, "MobileUpdate", PacketDirectionType.Output, 19);
        Add(0x21, "MovementReject", PacketDirectionType.Output, 8);
        Add(0x22, "MovementAck", PacketDirectionType.Input | PacketDirectionType.Output, 3);
        Add(0x2C, "DeathStatus", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0x34, "MobileStatusQuery", PacketDirectionType.Input, 10);
        Add(0x3A, "Skills", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x3B, "BuyItems", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0x55, "LoginComplete", PacketDirectionType.Output, 1);
        Add(0x5D, "CharacterSelect", PacketDirectionType.Input, 73);
        Add(0x6C, "TargetResponse", PacketDirectionType.Input | PacketDirectionType.Output, 19);
        Add(0x72, "WarMode", PacketDirectionType.Input | PacketDirectionType.Output, 5);
        Add(0x73, "Ping", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0x80, "AccountLoginRequest", PacketDirectionType.Input, 62);
        Add(0x82, "LoginDenied", PacketDirectionType.Output, 2);
        Add(0x8C, "ConnectToGameServer", PacketDirectionType.Output, 11);
        Add(0x91, "GameServerLogin", PacketDirectionType.Input, 65);
        Add(0x9B, "HelpRequest", PacketDirectionType.Input, 258);
        Add(0x9F, "SellItems", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xA0, "SelectServer", PacketDirectionType.Input, 3);
        Add(0xA4, "ClientSpy", PacketDirectionType.Input, 149);
        Add(0xA8, "ServerList", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xA9, "CharacterList", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xAD, "UnicodeSpeech", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xAE, "UnicodeMessage", PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xB1, "GumpResponse", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xB5, "OpenChatWindow", PacketDirectionType.Input, 64);
        Add(0xB8, "Profile", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xB9, "SupportedFeatures", PacketDirectionType.Output, 5);
        Add(0xBD, "ClientVersion", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xBF, "ExtendedCommand", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xC8, "ClientViewRange", PacketDirectionType.Input | PacketDirectionType.Output, 2);
        Add(0xD6, "MegaCliloc", PacketDirectionType.Input | PacketDirectionType.Output, PacketLengths.Variable);
        Add(0xD7, "GenericAosCommand", PacketDirectionType.Input, PacketLengths.Variable);
        Add(0xEF, "LoginSeed", PacketDirectionType.Input, 21);
        Add(0xF8, "CharacterCreationNew", PacketDirectionType.Input, 106);

        return table;
    }
}
