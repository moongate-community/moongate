using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Server.Utils;

internal static class ItemImageNormalizer
{
    private const int DefaultPadding = 4;

    public static Image<Rgba32> CropAndPad(Image<Rgba32> source, int padding = DefaultPadding)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!TryFindOpaqueBounds(source, out var minX, out var minY, out var maxX, out var maxY))
        {
            return source.Clone();
        }

        var croppedWidth = maxX - minX + 1;
        var croppedHeight = maxY - minY + 1;
        var outputWidth = croppedWidth + (padding * 2);
        var outputHeight = croppedHeight + (padding * 2);
        var output = new Image<Rgba32>(outputWidth, outputHeight);

        for (var y = 0; y < croppedHeight; y++)
        {
            for (var x = 0; x < croppedWidth; x++)
            {
                output[x + padding, y + padding] = source[minX + x, minY + y];
            }
        }

        return output;
    }

    private static bool TryFindOpaqueBounds(
        Image<Rgba32> source,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY
    )
    {
        minX = source.Width;
        minY = source.Height;
        maxX = -1;
        maxY = -1;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (source[x, y].A == 0)
                {
                    continue;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        return maxX >= 0;
    }
}
