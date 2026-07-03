using System.IO;
using Moongate.Ultima.Imaging;

// ascii text support written by arul
namespace Moongate.Ultima;

public static class AsciiText
{
    public static readonly AsciiFont[] Fonts = new AsciiFont[10];

    static AsciiText()
    {
        Initialize();
    }

    /// <summary>
    /// Reads fonts.mul
    /// </summary>
    public static unsafe void Initialize()
    {
        string path = Files.GetFilePath("fonts.mul");
        if (path == null)
        {
            return;
        }

        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var buffer = new byte[(int)reader.Length];
            reader.ReadExactly(buffer, 0, (int)reader.Length);
            fixed (byte* bin = buffer)
            {
                byte* read = bin;
                for (int i = 0; i < 10; ++i)
                {
                    byte header = *read++;
                    Fonts[i] = new AsciiFont(header);

                    for (int k = 0; k < 224; ++k)
                    {
                        byte width = *read++;
                        byte height = *read++;
                        byte unk = *read++; // delimiter?

                        if (width <= 0 || height <= 0)
                        {
                            continue;
                        }

                        if (height > Fonts[i].Height && k < 96)
                        {
                            Fonts[i].Height = height;
                        }

                        var bmp = new UltimaBitmap(width, height);
                        var line = (ushort*)bmp.Scan0;
                        int delta = bmp.Stride >> 1;

                        for (int y = 0; y < height; ++y, line += delta)
                        {
                            ushort* cur = line;
                            for (int x = 0; x < width; ++x)
                            {
                                var pixel = (ushort)(*read++ | (*read++ << 8));
                                if (pixel == 0)
                                {
                                    cur[x] = pixel;
                                }
                                else
                                {
                                    cur[x] = (ushort)(pixel ^ 0x8000);
                                }
                            }
                        }

                        Fonts[i].Characters[k] = bmp;
                        Fonts[i].Unk[k] = unk;
                    }
                }
            }
        }
    }

    public static unsafe void Save(string fileName)
    {
        using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
        using (var bin = new BinaryWriter(fs))
        {
            for (int i = 0; i < 10; ++i)
            {
                bin.Write(Fonts[i].Header);
                for (int k = 0; k < 224; ++k)
                {
                    bin.Write((byte)Fonts[i].Characters[k].Width);
                    bin.Write((byte)Fonts[i].Characters[k].Height);
                    bin.Write(Fonts[i].Unk[k]);
                    UltimaBitmap bmp = Fonts[i].Characters[k];
                    var line = (ushort*)bmp.Scan0;
                    int delta = bmp.Stride >> 1;
                    for (int y = 0; y < bmp.Height; ++y, line += delta)
                    {
                        ushort* cur = line;
                        for (int x = 0; x < bmp.Width; ++x)
                        {
                            if (cur[x] == 0)
                            {
                                bin.Write(cur[x]);
                            }
                            else
                            {
                                bin.Write((ushort)(cur[x] ^ 0x8000));
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws Text with font in Bitmap and returns
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="text"></param>
    /// <returns></returns>
    public static UltimaBitmap DrawText(int fontId, string text)
    {
        AsciiFont font = AsciiFont.GetFixed(fontId, Fonts);
        var result = new UltimaBitmap(font.GetWidth(text) + 2, font.Height + 2);

        int dx = 2;
        int dy = font.Height + 2;
        foreach (var character in text)
        {
            UltimaBitmap bmp = font.GetBitmap(character);
            bmp.DrawInto(result, dx, dy - bmp.Height);
            dx += bmp.Width;
        }

        return result;
    }
}
