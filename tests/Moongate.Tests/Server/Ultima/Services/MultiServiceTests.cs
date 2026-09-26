using System.Buffers.Binary;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Files;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Server.Ultima.Services;

[Collection(UltimaFilesCollection.Name)]
public sealed class MultiServiceTests
{
    [Fact]
    public async Task GetMulti_MulFiles_ReadsComponentsAndBounds()
    {
        using var client = CreateMulClient();
        var service = await StartAsync(client);

        var tent = service.GetMulti(2);

        Assert.Equal(1, service.Count);
        Assert.Equal(2, tent.Id);
        Assert.Equal(3, tent.Components.Count);
        Assert.Equal((ushort)0x0001, tent.Components[0].ItemId);
        Assert.False(tent.Components[0].Visible);
        Assert.Equal((ushort)0x0064, tent.Components[1].ItemId);
        Assert.Equal((-1, 2, 0), (tent.Components[1].Offset.X, tent.Components[1].Offset.Y, tent.Components[1].Offset.Z));
        Assert.True(tent.Components[1].Visible);
        Assert.Equal((-1, -3), (tent.Min.X, tent.Min.Y));
        Assert.Equal((4, 2), (tent.Max.X, tent.Max.Y));
        Assert.Equal(20, tent.Height);
    }

    [Fact]
    public async Task TryGetMulti_UnknownId_ReturnsFalse()
    {
        using var client = CreateMulClient();
        var service = await StartAsync(client);

        Assert.False(service.TryGetMulti(0, out _));
        Assert.Throws<KeyNotFoundException>(() => service.GetMulti(0));
    }

    [Fact]
    public async Task StartAsync_NoMultiFiles_ThrowsFileNotFoundException()
    {
        using var client = new TemporaryDirectory();

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => StartAsync(client));

        Assert.Contains("MultiCollection.uop", exception.Message);
    }

    private static async Task<MultiService> StartAsync(TemporaryDirectory client)
    {
        Files.SetDirectory(client.Path);
        var service = new MultiService();
        await service.StartAsync();

        return service;
    }

    // Multi 2 in the pre-High Seas format: a hidden centre marker and two tiles.
    private static TemporaryDirectory CreateMulClient()
    {
        var client = new TemporaryDirectory();
        byte[] components =
        [
            .. Component(0x0001, 0, 0, 0, 0),
            .. Component(0x0064, -1, 2, 0, 1),
            .. Component(0x0065, 4, -3, 20, 1)
        ];
        File.WriteAllBytes(Path.Combine(client.Path, "multi.mul"), components);

        var index = new byte[12 * 3];

        for (var id = 0; id < 3; id++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(id * 12), id == 2 ? 0 : -1);
            BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(id * 12 + 4), id == 2 ? components.Length : -1);
        }

        File.WriteAllBytes(Path.Combine(client.Path, "multi.idx"), index);

        return client;
    }

    private static byte[] Component(ushort id, short x, short y, short z, int flags)
    {
        var entry = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(entry, id);
        BinaryPrimitives.WriteInt16LittleEndian(entry.AsSpan(2), x);
        BinaryPrimitives.WriteInt16LittleEndian(entry.AsSpan(4), y);
        BinaryPrimitives.WriteInt16LittleEndian(entry.AsSpan(6), z);
        BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(8), flags);

        return entry;
    }
}
