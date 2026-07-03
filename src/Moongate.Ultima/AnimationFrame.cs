using System.IO;
using Moongate.Ultima.Imaging;
using SkiaSharp;

namespace Moongate.Ultima;

public sealed class AnimationFrame
{
    public SKPointI Center { get; set; }
    public UltimaBitmap Bitmap { get; set; }

    private const int _doubleXor = (0x200 << 22) | (0x200 << 12);

    public static readonly AnimationFrame Empty = new AnimationFrame();
    //public static readonly AnimationFrame[] EmptyFrames = new AnimationFrame[1] { Empty };

    private AnimationFrame()
    {
        Bitmap = new UltimaBitmap(1, 1);
    }

    public unsafe AnimationFrame(ushort[] palette, BinaryReader bin, bool flip)
    {
        int xCenter = bin.ReadInt16();
        int yCenter = bin.ReadInt16();

        int width = bin.ReadUInt16();
        int height = bin.ReadUInt16();
        if (height == 0 || width == 0)
        {
            return;
        }

        var bmp = new UltimaBitmap(width, height);
        var line = (ushort*)bmp.Scan0;
        int delta = bmp.Stride >> 1;

        int header;

        int xBase = xCenter - 0x200;
        int yBase = (yCenter + height) - 0x200;

        if (!flip)
        {
            line += xBase;
            line += yBase * delta;

            while ((header = bin.ReadInt32()) != 0x7FFF7FFF)
            {
                header ^= _doubleXor;

                ushort* cur = line + ((((header >> 12) & 0x3FF) * delta) + ((header >> 22) & 0x3FF));
                ushort* end = cur + (header & 0xFFF);
                while (cur < end)
                {
                    *cur++ = palette[bin.ReadByte()];
                }
            }
        }
        else
        {
            line -= xBase - width + 1;
            line += yBase * delta;

            while ((header = bin.ReadInt32()) != 0x7FFF7FFF)
            {
                header ^= _doubleXor;

                ushort* cur = line + ((((header >> 12) & 0x3FF) * delta) - ((header >> 22) & 0x3FF));
                ushort* end = cur - (header & 0xFFF);

                while (cur > end)
                {
                    *cur-- = palette[bin.ReadByte()];
                }
            }

            xCenter = width - xCenter;
        }

        Center = new SKPointI(xCenter, yCenter);
        Bitmap = bmp;
    }
}
