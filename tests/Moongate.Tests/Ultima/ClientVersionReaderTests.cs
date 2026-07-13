using System.Buffers.Binary;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class ClientVersionReaderTests
{
    private static ReadOnlySpan<byte> Signature =>
    [
        0x56, 0x00, 0x53, 0x00, 0x5F, 0x00, 0x56, 0x00,
        0x45, 0x00, 0x52, 0x00, 0x53, 0x00, 0x49, 0x00,
        0x4F, 0x00, 0x4E, 0x00, 0x5F, 0x00, 0x49, 0x00,
        0x4E, 0x00, 0x46, 0x00, 0x4F, 0x00
    ];

    // A minimal client.exe: prefix + VS_VERSION_INFO signature(30) + filler(12) + version fields
    // (minor, major, private, build as LE UInt16) encoding 7.0.95.3.
    private static byte[] BuildClientExe()
    {
        var buffer = new byte[8 + 30 + 12 + 8];
        Signature.CopyTo(buffer.AsSpan(8));
        var fields = buffer.AsSpan(8 + 30 + 12);
        BinaryPrimitives.WriteUInt16LittleEndian(fields, 0); // minor
        BinaryPrimitives.WriteUInt16LittleEndian(fields[2..], 7); // major
        BinaryPrimitives.WriteUInt16LittleEndian(fields[4..], 3); // private -> revision
        BinaryPrimitives.WriteUInt16LittleEndian(fields[6..], 95); // build

        return buffer;
    }

    [Fact]
    public void TryRead_WithVersionResource_ParsesVersion()
    {
        var ok = ClientVersionReader.TryRead(BuildClientExe(), out var version);

        Assert.True(ok);
        Assert.Equal(new Version(7, 0, 95, 3), version);
    }

    [Fact]
    public void TryRead_WithoutSignature_ReturnsFalse()
    {
        var buffer = new byte[128];

        var ok = ClientVersionReader.TryRead(buffer, out var version);

        Assert.False(ok);
        Assert.Equal(new Version(0, 0), version);
    }

    [Fact]
    public void Read_ResolvesClientExeFromFiles()
    {
        var dir = UltimaFixtures.CreateClientDirectory(("client.exe", BuildClientExe()));

        try
        {
            Files.SetDirectory(dir);

            Assert.Equal(new Version(7, 0, 95, 3), ClientVersionReader.Read());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Read_WhenClientExeMissing_ReturnsNull()
    {
        var dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", [1]));

        try
        {
            Files.SetDirectory(dir);

            Assert.Null(ClientVersionReader.Read());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
