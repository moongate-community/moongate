namespace Moongate.Network.Protocol;

/// <summary>
/// Client-to-server packet sizes for protocol 7.x. <see cref="Variable"/> means the
/// total length travels as a big-endian ushort at bytes 1-2; <see cref="Unknown"/>
/// means the id is not part of the protocol we accept (framing error).
/// The table starts with the ids the milestone-1 flow needs plus the common client
/// chatter; extending it is a one-line change per id.
/// </summary>
public static class PacketLengths
{
    public const short Variable = -1;
    public const short Unknown = 0;

    private static readonly short[] _lengths = BuildTable();

    public static short Get(byte packetId)
    {
        return _lengths[packetId];
    }

    private static short[] BuildTable()
    {
        var table = new short[256];

        table[0x00] = 104;      // character creation (pre-7.0.16)
        table[0x01] = 5;        // disconnect notification
        table[0x02] = 7;        // move request
        table[0x03] = Variable; // ascii speech
        table[0x05] = 5;        // attack request
        table[0x06] = 5;        // double click
        table[0x07] = 7;        // pick up item
        table[0x08] = 15;       // drop item (6.0.1.7+)
        table[0x09] = 5;        // single click
        table[0x12] = Variable; // text command (skill/action use)
        table[0x13] = 10;       // equip item
        table[0x22] = 3;        // movement ack / resync request
        table[0x2C] = 2;        // death status response
        table[0x34] = 10;       // mobile status query
        table[0x3A] = Variable; // skills change
        table[0x3B] = Variable; // buy items
        table[0x5D] = 73;       // character select (play character)
        table[0x6C] = 19;       // target response
        table[0x72] = 5;        // war mode
        table[0x73] = 2;        // ping
        table[0x80] = 62;       // account login request
        table[0x82] = 2;        // login denied
        table[0x8C] = 11;       // connect to game server
        table[0x91] = 65;       // game server login
        table[0x9B] = 258;      // help request
        table[0x9F] = Variable; // sell items
        table[0xA0] = 3;        // select server
        table[0xA4] = 149;      // client spy (hardware info)
        table[0xA8] = Variable; // server list
        table[0xA9] = Variable; // character list
        table[0xAD] = Variable; // unicode speech
        table[0xB1] = Variable; // gump menu response
        table[0xB5] = 64;       // open chat window
        table[0xB8] = Variable; // profile request
        table[0xBD] = Variable; // client version
        table[0xBF] = Variable; // extended command
        table[0xC8] = 2;        // client view range
        table[0xD6] = Variable; // mega cliloc request
        table[0xD7] = Variable; // generic AOS command
        table[0xEF] = 21;       // login seed (ClassicUO)
        table[0xF8] = 106;      // character creation (7.x)

        return table;
    }
}
