using Moongate.UO.Data.Interfaces.Maps;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Tiles;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Server.Services.Maps;

/// <summary>
/// Renders UO map tiles into a radar-color PNG image using RadarCol and TileMatrix.
/// </summary>
public sealed class MapImageService : IMapImageService
{
    public Image? GetMapImage(int mapId)
    {
        var map = Map.GetMap(mapId);

        if (map is null)
        {
            return null;
        }

        var tiles = map.Tiles;

        if (tiles.MapStream is null)
        {
            return null;
        }

        var width = map.Width;
        var height = map.Height;
        var blockWidth = tiles.BlockWidth;
        var blockHeight = tiles.BlockHeight;

        var image = new Image<Rgb24>(width, height);

        for (var blockX = 0; blockX < blockWidth; blockX++)
        {
            for (var blockY = 0; blockY < blockHeight; blockY++)
            {
                var landBlock = tiles.GetLandBlock(blockX, blockY);
                var staticBlock = tiles.GetStaticBlock(blockX, blockY);

                for (var tileX = 0; tileX < 8; tileX++)
                {
                    for (var tileY = 0; tileY < 8; tileY++)
                    {
                        var px = blockX * 8 + tileX;
                        var py = blockY * 8 + tileY;

                        if (px >= width || py >= height)
                        {
                            continue;
                        }

                        var land = landBlock[(tileY << 3) + tileX];
                        var topZ = land.Z;
                        var (r, g, b) = land.ID > 0
                                            ? RadarCol.GetLandColor(land.ID)
                                            : ((byte)0, (byte)0, (byte)0);

                        var statics = staticBlock[tileX][tileY];

                        foreach (ref readonly var s in statics.AsSpan())
                        {
                            var top = s.Z + s.Height;

                            if (top >= topZ)
                            {
                                topZ = top;
                                (r, g, b) = RadarCol.GetStaticColor(s.ID);
                            }
                        }

                        image[px, py] = new(r, g, b);
                    }
                }
            }
        }

        return image;
    }
}
