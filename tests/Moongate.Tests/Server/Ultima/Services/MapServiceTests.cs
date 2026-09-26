using System.Buffers.Binary;
using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Files;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Io;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

[Collection(UltimaFilesCollection.Name)]
public sealed class MapServiceTests
{
    [Fact]
    public async Task GetLand_OneBlockMap_ReadsIdAndZ()
    {
        using var client = CreateClient();
        var service = await StartAsync(client);

        var land = service.GetLand(MapType.Felucca, 3, 5);

        Assert.Equal((ushort)(0x10 + 5 * 8 + 3), land.Id);
        Assert.Equal((sbyte)-5, land.Z);
    }

    [Fact]
    public async Task GetStatics_CellWithTwoObjects_ReadsThemInFileOrder()
    {
        using var client = CreateClient();
        var service = await StartAsync(client);

        var statics = service.GetStatics(MapType.Felucca, 2, 6);

        Assert.Equal(2, statics.Count);
        Assert.Equal((0x0064, 10, 0), (statics[0].Id, statics[0].Z, statics[0].Hue));
        Assert.Equal((0x0065, 30, 0x21), (statics[1].Id, statics[1].Z, statics[1].Hue));
        Assert.Empty(service.GetStatics(MapType.Felucca, 0, 0));
    }

    [Fact]
    public async Task Contains_AndOutOfRange_FollowTheMapSize()
    {
        using var client = CreateClient();
        var service = await StartAsync(client);

        Assert.Equal([MapType.Felucca], service.Maps);
        Assert.True(service.Contains(MapType.Felucca, 7, 7));
        Assert.False(service.Contains(MapType.Felucca, 8, 0));
        Assert.False(service.Contains(MapType.Trammel, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetLand(MapType.Felucca, -1, 0));
        Assert.Throws<KeyNotFoundException>(() => service.GetStatics(MapType.Trammel, 0, 0));
    }

    [Fact]
    public async Task StartAsync_MissingStatics_ThrowsFileNotFoundException()
    {
        using var client = CreateClient();
        File.Delete(Path.Combine(client.Path, "statics0.mul"));

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => StartAsync(client));

        Assert.Contains("statics0.mul", exception.Message);
    }

    private static async Task<MapService> StartAsync(TemporaryDirectory client)
    {
        Files.SetDirectory(client.Path);
        var dataLoaderService = new StubDataLoaderService().With(
            new MapContent { Map = MapType.Felucca, FileIndex = 0, Name = "Felucca", Size = new Point2D(8, 8) }
        );
        var service = new MapService(dataLoaderService);
        await service.StartAsync();

        return service;
    }

    // One 8x8 block: land id 0x10 + index with Z of minus the row, and two statics on cell (2, 6).
    private static TemporaryDirectory CreateClient()
    {
        var client = new TemporaryDirectory();
        var map = new byte[196];

        for (var cell = 0; cell < 64; cell++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(4 + cell * 3), (ushort)(0x10 + cell));
            map[4 + cell * 3 + 2] = unchecked((byte)(sbyte)-(cell / 8));
        }

        File.WriteAllBytes(Path.Combine(client.Path, "map0.mul"), map);

        byte[] statics = [.. Static(0x0064, 2, 6, 10, 0), .. Static(0x0065, 2, 6, 30, 0x21)];
        File.WriteAllBytes(Path.Combine(client.Path, "statics0.mul"), statics);

        var index = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(0), 0);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(4), statics.Length);
        File.WriteAllBytes(Path.Combine(client.Path, "staidx0.mul"), index);

        return client;
    }

    private static byte[] Static(ushort id, byte x, byte y, sbyte z, short hue)
    {
        var entry = new byte[7];
        BinaryPrimitives.WriteUInt16LittleEndian(entry, id);
        entry[2] = x;
        entry[3] = y;
        entry[4] = unchecked((byte)z);
        BinaryPrimitives.WriteInt16LittleEndian(entry.AsSpan(5), hue);

        return entry;
    }
}
