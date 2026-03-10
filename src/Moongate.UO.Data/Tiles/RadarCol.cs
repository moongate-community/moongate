using Moongate.UO.Data.Files;

namespace Moongate.UO.Data.Tiles;

/// <summary>
/// Reads radarcol.mul and provides radar-color lookups for land and static tiles.
/// </summary>
public static class RadarCol
{
    private const int TotalEntries = 0x8000;

    public static ushort[] Colors { get; } = Load();

    public static (byte R, byte G, byte B) GetLandColor(int tileId)
    {
        var color = Colors[tileId & 0x3FFF];
        return FromRgb555(color);
    }

    public static (byte R, byte G, byte B) GetStaticColor(int tileId)
    {
        var color = Colors[(tileId & 0x3FFF) + 0x4000];
        return FromRgb555(color);
    }

    public static (byte R, byte G, byte B) FromRgb555(ushort color)
    {
        var r = (color >> 10) & 0x1F;
        var g = (color >> 5) & 0x1F;
        var b = color & 0x1F;
        return (
            (byte)((r << 3) | (r >> 2)),
            (byte)((g << 3) | (g >> 2)),
            (byte)((b << 3) | (b >> 2))
        );
    }

    private static ushort[] Load()
    {
        var path = UoFiles.FindDataFile("radarcol.mul", false);

        if (path is null)
        {
            return new ushort[TotalEntries];
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        var colors = new ushort[TotalEntries];
        var count = Math.Min(TotalEntries, (int)(stream.Length / 2));

        for (var i = 0; i < count; i++)
        {
            colors[i] = reader.ReadUInt16();
        }

        return colors;
    }
}
