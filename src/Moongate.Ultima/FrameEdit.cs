using System.Collections.Generic;
using System.IO;
using Moongate.Ultima.Imaging;
using SkiaSharp;

namespace Moongate.Ultima;

public sealed class FrameEdit
{
    private const int _doubleXor = (0x200 << 22) | (0x200 << 12);

    public struct Raw
    {
        public int run;
        public int offsetX;
        public int offsetY;
        public byte[] data;
    }

    public Raw[] RawData { get; }
    public SKPointI Center { get; set; }

    public readonly int Width;
    public readonly int Height;

    public FrameEdit(BinaryReader bin)
    {
        int xCenter = bin.ReadInt16();
        int yCenter = bin.ReadInt16();

        Width = bin.ReadUInt16();
        Height = bin.ReadUInt16();

        if (Height == 0 || Width == 0)
        {
            return;
        }

        int header;

        var tmp = new List<Raw>();

        while ((header = bin.ReadInt32()) != 0x7FFF7FFF)
        {
            var raw = new Raw();
            header ^= _doubleXor;
            raw.run = (header & 0xFFF);
            raw.offsetY = ((header >> 12) & 0x3FF);
            raw.offsetX = ((header >> 22) & 0x3FF);

            int i = 0;
            raw.data = new byte[raw.run];

            while (i < raw.run)
            {
                raw.data[i++] = bin.ReadByte();
            }

            tmp.Add(raw);
        }

        RawData = tmp.ToArray();
        Center = new SKPointI(xCenter, yCenter);
    }

    public unsafe FrameEdit(UltimaBitmap bit, ushort[] palette, int centerX, int centerY)
    {
        Center = new SKPointI(centerX, centerY);
        Width = bit.Width;
        Height = bit.Height;

        var line = (ushort*)bit.Scan0;
        int delta = bit.Stride >> 1;
        var tmp = new List<Raw>();

        for (int y = 0; y < bit.Height; ++y, line += delta)
        {
            ushort* cur = line;

            int i = 0;
            int x = 0;

            while (i < bit.Width)
            {
                for (i = x; i <= bit.Width; ++i)
                {
                    // first pixel set
                    if (i < bit.Width && cur[i] != 0)
                    {
                        break;
                    }
                }

                if (i >= bit.Width)
                {
                    continue;
                }

                int j;
                for (j = (i + 1); j < bit.Width; ++j)
                {
                    // next non set pixel
                    if (cur[j] == 0)
                    {
                        break;
                    }
                }

                var raw = new Raw
                {
                    run = j - i
                };
                raw.offsetX = j - raw.run - centerX;
                raw.offsetX += 512;
                raw.offsetY = y - centerY - bit.Height;
                raw.offsetY += 512;

                int r = 0;
                raw.data = new byte[raw.run];
                while (r < raw.run)
                {
                    ushort col = cur[r + i];
                    raw.data[r++] = GetPaletteIndex(palette, col);
                }
                tmp.Add(raw);
                x = j + 1;
                i = x;
            }
        }

        RawData = tmp.ToArray();
    }

    public void ChangeCenter(int x, int y)
    {
        for (int i = 0; i < RawData.Length; i++)
        {
            RawData[i].offsetX += Center.X;
            RawData[i].offsetX -= x;
            RawData[i].offsetY += Center.Y;
            RawData[i].offsetY -= y;
        }

        Center = new SKPointI(x, y);
    }

    private static byte GetPaletteIndex(IReadOnlyList<ushort> palette, ushort col)
    {
        for (int i = 0; i < palette.Count; i++)
        {
            if (palette[i] == col)
            {
                return (byte)i;
            }
        }

        return 0;
    }

    public void Save(BinaryWriter bin)
    {
        bin.Write((short)Center.X);
        bin.Write((short)Center.Y);
        bin.Write((ushort)Width);
        bin.Write((ushort)Height);

        if (RawData != null)
        {
            for (int j = 0; j < RawData.Length; j++)
            {
                int newHeader = RawData[j].run | (RawData[j].offsetY << 12) | (RawData[j].offsetX << 22);
                newHeader ^= _doubleXor;
                bin.Write(newHeader);
                foreach (byte b in RawData[j].data)
                {
                    bin.Write(b);
                }
            }
        }

        bin.Write(0x7FFF7FFF);
    }
}
