using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Imaging;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Ultima;

public sealed class Hue
{
    public int Index { get; }
    public ushort[] Colors { get; }
    public string Name { get; set; }
    public ushort TableStart { get; set; }
    public ushort TableEnd { get; set; }

    public Hue(int index)
    {
        Name = "Null";
        Index = index;
        Colors = new ushort[32];
        TableStart = 0;
        TableEnd = 0;
    }

    public Rgba32 GetColor(int index)
    {
        return HueToColor(Colors[index]);
    }

    /// <summary>
    /// Converts Hue color to RGB color
    /// </summary>
    /// <param name="hue"></param>
    private static Rgba32 HueToColor(ushort hue)
    {
        const int scale = 255 / 31;

        return new Rgba32(
            (byte)(((hue & 0x7c00) >> 10) * scale),
            (byte)(((hue & 0x3e0) >> 5) * scale),
            (byte)((hue & 0x1f) * scale));
    }

    private static readonly byte[] _stringBuffer = new byte[20];

    public Hue(int index, BinaryReader bin)
    {
        Index = index;
        Colors = new ushort[32];

        byte[] buffer = bin.ReadBytes(88);
        unsafe
        {
            fixed (byte* bufferPtr = buffer)
            {
                var buf = (ushort*)bufferPtr;
                for (int i = 0; i < 32; ++i)
                {
                    Colors[i] = *buf++;
                }

                TableStart = *buf++;
                TableEnd = *buf++;

                var stringBuffer = (byte*)buf;
                int count;
                for (count = 0; count < 20 && *stringBuffer != 0; ++count)
                {
                    _stringBuffer[count] = *stringBuffer++;
                }

                Name = Encoding.ASCII.GetString(_stringBuffer, 0, count);
                Name = Name.Replace("\n", " ");
            }
        }
    }

    public Hue(int index, HueDataMul mulStruct)
    {
        Index = index;
        Colors = new ushort[32];
        for (int i = 0; i < 32; ++i)
        {
            ushort c = mulStruct.colors[i];
            // Clamp c == 0 or any value with the high bit set to 1. The high bit is a
            // flag in this format, never part of a valid color value.
            if (c == 0 || c > 0x7fff)
            {
                c = 1;
            }

            Colors[i] = c;
        }

        TableStart = mulStruct.tableStart;
        TableEnd = mulStruct.tableEnd;

        Name = TileDataHelpers.ReadNameString(mulStruct.name, 20);
        Name = Name.Replace("\n", " ");
    }

    /// <summary>
    /// Builds a Hue directly from the on-disk byte layout: 32 ushorts of
    /// colors, then tableStart / tableEnd ushorts, then a 20-byte ASCII
    /// name. Lets the loader skip Marshal.PtrToStructure boxing per hue.
    /// </summary>
    public Hue(int index, ReadOnlySpan<byte> data)
    {
        Index = index;
        Colors = new ushort[32];
        for (int i = 0; i < 32; ++i)
        {
            ushort c = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * 2));
            if (c == 0 || c > 0x7fff)
            {
                c = 1;
            }

            Colors[i] = c;
        }

        TableStart = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(64));
        TableEnd = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(66));

        Name = TileDataHelpers.ReadNameString(data.Slice(68, 20));
        Name = Name.Replace("\n", " ");
    }

    /// <summary>
    /// Applies Hue to Bitmap
    /// </summary>
    /// <param name="bmp"></param>
    /// <param name="onlyHueGrayPixels"></param>
    public unsafe void ApplyTo(UltimaBitmap bmp, bool onlyHueGrayPixels)
    {
        int stride = bmp.Stride >> 1;
        int width = bmp.Width;
        int height = bmp.Height;
        int delta = stride - width;

        var pBuffer = (ushort*)bmp.Scan0;
        ushort* pLineEnd = pBuffer + width;
        ushort* pImageEnd = pBuffer + (stride * height);

        if (onlyHueGrayPixels)
        {
            while (pBuffer < pImageEnd)
            {
                while (pBuffer < pLineEnd)
                {
                    int c = *pBuffer;
                    if (c != 0)
                    {
                        int r = (c >> 10) & 0x1F;
                        int g = (c >> 5) & 0x1F;
                        int b = c & 0x1F;
                        if (r == g && r == b)
                        {
                            *pBuffer = (ushort)(Colors[(c >> 10) & 0x1F] | 0x8000);
                        }
                    }
                    ++pBuffer;
                }

                pBuffer += delta;
                pLineEnd += stride;
            }
        }
        else
        {
            while (pBuffer < pImageEnd)
            {
                while (pBuffer < pLineEnd)
                {
                    if (*pBuffer != 0)
                    {
                        *pBuffer = (ushort)(Colors[(*pBuffer >> 10) & 0x1F] | 0x8000);
                    }

                    ++pBuffer;
                }

                pBuffer += delta;
                pLineEnd += stride;
            }
        }
    }

    public void Export(string fileName)
    {
        using (var tex = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite), Encoding.GetEncoding(1252)))
        {
            tex.WriteLine(Name);
            tex.WriteLine(TableStart.ToString());
            tex.WriteLine(TableEnd.ToString());

            foreach (var colorValue in Colors)
            {
                tex.WriteLine(colorValue.ToString());
            }
        }
    }

    public void Import(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return;
        }

        using (var sr = new StreamReader(fileName))
        {
            int i = -3;

            while (sr.ReadLine() is { } line)
            {
                line = line.Trim();

                try
                {
                    if (i >= Colors.Length)
                    {
                        break;
                    }

                    switch (i)
                    {
                        case -3:
                            Name = line;
                            break;
                        case -2:
                            TableStart = ushort.Parse(line);
                            break;
                        case -1:
                            TableEnd = ushort.Parse(line);
                            break;
                        default:
                            Colors[i] = ushort.Parse(line);
                            break;
                    }

                    ++i;
                }
                catch
                {
                    // TODO: ignored?
                    // ignored
                }
            }
        }
    }
}
