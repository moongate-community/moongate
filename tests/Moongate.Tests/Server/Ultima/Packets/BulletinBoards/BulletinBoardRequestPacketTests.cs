using System.Text;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.BulletinBoards;
using Moongate.Server.Ultima.Types.BulletinBoards;

namespace Moongate.Tests.Server.Ultima.Packets.BulletinBoards;

public sealed class BulletinBoardRequestPacketTests
{
    [Theory,
     InlineData("03", BulletinBoardCommandType.RequestMessage),
     InlineData("04", BulletinBoardCommandType.RequestSummary),
     InlineData("06", BulletinBoardCommandType.Remove)]
    public void TryParse_ARequestAboutOneMessage_HasTheBoardAndTheMessage(string command, BulletinBoardCommandType expected)
    {
        Assert.True(BulletinBoardRequestPacket.TryParse(Convert.FromHexString("71000C" + command + "40000001" + "40000010"), out var packet));

        Assert.Equal((expected, new Serial(0x40000001), new Serial(0x40000010)), (packet.Command, packet.Board, packet.Message));
        Assert.Empty(packet.Lines);
    }

    [Fact]
    public void TryParse_APost_HasWhatItRepliesTo_TheSubjectAndTheLines()
    {
        var data = Post("40000010", ("Re: Horse", 1), [("How much?", 1), ("", 1), ("Bruno", 1)]);

        Assert.True(BulletinBoardRequestPacket.TryParse(data, out var packet));

        Assert.Equal((BulletinBoardCommandType.Post, new Serial(0x40000001), new Serial(0x40000010)), (packet.Command, packet.Board, packet.Message));
        Assert.Equal("Re: Horse", packet.Subject);
        Assert.Equal(["How much?", "", "Bruno"], packet.Lines);
    }

    // The client counts its zero in the length, and old ones two of them: the text ends at the first.
    [Fact]
    public void TryParse_APost_ReadsEachStringUpToItsFirstZero()
    {
        var data = Post("00000000", ("Horse", 1), [("Selling", 2)]);

        Assert.True(BulletinBoardRequestPacket.TryParse(data, out var packet));

        Assert.Equal(("Horse", Serial.Zero), (packet.Subject, packet.Message));
        Assert.Equal(["Selling"], packet.Lines);
    }

    [Fact]
    public void TryParse_APost_IsUtf8()
    {
        Assert.True(BulletinBoardRequestPacket.TryParse(Post("00000000", ("Caffè", 1), [("perché", 1)]), out var packet));

        Assert.Equal(("Caffè", "perché"), (packet.Subject, packet.Lines[0]));
    }

    [Fact]
    public void TryParse_APostWithNoSubjectAndNoLine_Parses_ForTheServiceToRefuse()
    {
        Assert.True(BulletinBoardRequestPacket.TryParse(Convert.FromHexString("71000E" + "05" + "40000001" + "00000000" + "00" + "00"), out var packet));

        Assert.Equal("", packet.Subject);
        Assert.Empty(packet.Lines);
    }

    [Theory,
     // The subject says ten bytes and three follow.
     InlineData("710010" + "05" + "40000001" + "00000000" + "0A" + "414243"),
     // Two lines announced, one there.
     InlineData("710013" + "05" + "40000001" + "00000000" + "02" + "4100" + "02" + "02" + "4100"),
     // A line that says five bytes and has two.
     InlineData("710012" + "05" + "40000001" + "00000000" + "02" + "4100" + "01" + "05" + "4100"),
     // No subject length at all.
     InlineData("71000C" + "05" + "40000001" + "00000000")]
    public void TryParse_APostWhoseLengthsRunPastThePacket_IsRefused(string hex)
    {
        Assert.False(BulletinBoardRequestPacket.TryParse(Convert.FromHexString(hex), out _));
    }

    [Theory,
     // What the server sends is not what a client asks.
     InlineData("71000C" + "00" + "40000001" + "40000010"),
     InlineData("71000C" + "02" + "40000001" + "40000010"),
     InlineData("71000C" + "09" + "40000001" + "40000010"),
     // Too short for a message serial.
     InlineData("710008" + "03" + "40000001"),
     // The length field lies.
     InlineData("710020" + "03" + "40000001" + "40000010")]
    public void TryParse_WhatIsNotARequest_IsRefused(string hex)
    {
        Assert.False(BulletinBoardRequestPacket.TryParse(Convert.FromHexString(hex), out _));
    }

    // A post as the client sends it: each string with its length, which counts the zeros after the text.
    private static byte[] Post(string replyTo, (string Text, int Zeros) subject, (string Text, int Zeros)[] lines)
    {
        var body = new List<byte>();
        body.Add(0x05);
        body.AddRange(Convert.FromHexString("40000001" + replyTo));
        Add(body, subject);
        body.Add((byte)lines.Length);

        foreach (var line in lines)
        {
            Add(body, line);
        }

        var length = body.Count + 3;

        return [0x71, (byte)(length >> 8), (byte)length, .. body];
    }

    private static void Add(List<byte> body, (string Text, int Zeros) value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Text);
        body.Add((byte)(bytes.Length + value.Zeros));
        body.AddRange(bytes);
        body.AddRange(new byte[value.Zeros]);
    }
}
