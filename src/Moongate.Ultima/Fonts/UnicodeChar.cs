using Moongate.Ultima.Imaging;

namespace Moongate.Ultima.Fonts;

public sealed class UnicodeChar
{
    public byte[] Bytes { get; set; }
    public sbyte XOffset { get; set; }
    public sbyte YOffset { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }

    /// <summary>
    /// Gets Bitmap of Char with Background -1
    /// </summary>
    /// <param name="fill"></param>
    /// <returns></returns>
    public unsafe UltimaBitmap GetImage(bool fill = false)
    {
        if (Width == 0 || Height == 0)
        {
            return null;
        }

        var bmp = new UltimaBitmap(Width, Height);
        var line = (ushort*)bmp.Scan0;
        var delta = bmp.Stride >> 1;

        for (var y = 0; y < Height; ++y, line += delta)
        {
            var cur = line;

            for (var x = 0; x < Width; ++x)
            {
                if (IsPixelSet(Bytes, Width, x, y))
                {
                    cur[x] = 0x8000;
                }
                else if (fill)
                {
                    cur[x] = 0xffff;
                }
            }
        }

        return bmp;
    }

    /// <summary>
    /// Resets Buffer with Bitmap
    /// </summary>
    /// <param name="bmp"></param>
    public unsafe void SetBuffer(UltimaBitmap bmp)
    {
        Bytes = new byte[bmp.Height * ((bmp.Width - 1) / 8 + 1)];

        XOffset = 0;
        YOffset = 0;

        Width = bmp.Width;
        Height = bmp.Height;

        var line = (ushort*)bmp.Scan0;
        var delta = bmp.Stride >> 1;

        for (var y = 0; y < bmp.Height; ++y, line += delta)
        {
            var cur = line;

            for (var x = 0; x < bmp.Width; ++x)
            {
                if (cur[x] != 0x8000)
                {
                    continue;
                }

                var offset = x / 8 + y * ((bmp.Width + 7) / 8);
                Bytes[offset] |= (byte)(1 << (7 - x % 8));
            }
        }
    }

    private static bool IsPixelSet(byte[] data, int width, int x, int y)
    {
        var offset = x / 8 + y * ((width + 7) / 8);

        if (offset > data.Length)
        {
            return false;
        }

        return (data[offset] & (1 << (7 - x % 8))) != 0;
    }
}
