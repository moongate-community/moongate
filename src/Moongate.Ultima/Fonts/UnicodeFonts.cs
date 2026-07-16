using Moongate.Ultima.Imaging;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Fonts;

public static class UnicodeFonts
{
    private static readonly string[] _files =
    {
        "unifont.mul",
        "unifont1.mul",
        "unifont2.mul",
        "unifont3.mul",
        "unifont4.mul",
        "unifont5.mul",
        "unifont6.mul",
        "unifont7.mul",
        "unifont8.mul",
        "unifont9.mul",
        "unifont10.mul",
        "unifont11.mul",
        "unifont12.mul"
    };

    public static readonly UnicodeFont[] Fonts = new UnicodeFont[13];

    static UnicodeFonts()
    {
        Initialize();
    }

    /// <summary>
    /// Reads unifont*.mul
    /// </summary>
    public static void Initialize()
    {
        for (var i = 0; i < _files.Length; i++)
        {
            var filePath = Files.GetFilePath(_files[i]);

            if (filePath == null)
            {
                continue;
            }

            Fonts[i] = new();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var bin = new BinaryReader(fs))
                {
                    for (var c = 0; c < 0x10000; ++c)
                    {
                        Fonts[i].Chars[c] = new();
                        fs.Seek(c * 4, SeekOrigin.Begin);
                        var num2 = bin.ReadInt32();

                        if (num2 >= fs.Length || num2 <= 0)
                        {
                            continue;
                        }

                        fs.Seek(num2, SeekOrigin.Begin);

                        var xOffset = bin.ReadSByte();
                        var yOffset = bin.ReadSByte();
                        int width = bin.ReadByte();
                        int height = bin.ReadByte();

                        Fonts[i].Chars[c].XOffset = xOffset;
                        Fonts[i].Chars[c].YOffset = yOffset;
                        Fonts[i].Chars[c].Width = width;
                        Fonts[i].Chars[c].Height = height;

                        if (!(width == 0 || height == 0))
                        {
                            Fonts[i].Chars[c].Bytes = bin.ReadBytes(height * ((width - 1) / 8 + 1));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Saves Font and returns string Filename
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fileType"></param>
    /// <returns></returns>
    public static string Save(string path, int fileType)
    {
        var fileName = Path.Combine(path, _files[fileType]);

        using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var bin = new BinaryWriter(fs))
            {
                fs.Seek(0x10000 * 4, SeekOrigin.Begin);
                bin.Write(0);

                // Set first data
                for (var c = 0; c < 0x10000; ++c)
                {
                    if (Fonts[fileType].Chars[c].Bytes == null)
                    {
                        continue;
                    }

                    fs.Seek(c * 4, SeekOrigin.Begin);
                    bin.Write((int)fs.Length);
                    fs.Seek(fs.Length, SeekOrigin.Begin);
                    bin.Write(Fonts[fileType].Chars[c].XOffset);
                    bin.Write(Fonts[fileType].Chars[c].YOffset);
                    bin.Write((byte)Fonts[fileType].Chars[c].Width);
                    bin.Write((byte)Fonts[fileType].Chars[c].Height);
                    bin.Write(Fonts[fileType].Chars[c].Bytes);
                }
            }
        }

        return fileName;
    }

    /// <summary>
    /// Draws Text with font in Bitmap and returns
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="text"></param>
    /// <returns></returns>
    public static UltimaBitmap WriteText(int fontId, string text)
    {
        var result = new UltimaBitmap(Fonts[fontId].GetWidth(text) + 2, Fonts[fontId].GetHeight(text) + 2);

        var dx = 2;
        var dy = 2;

        foreach (var character in text)
        {
            var c = character % 0x10000;
            var bmp = Fonts[fontId].Chars[c].GetImage();
            dx += Fonts[fontId].Chars[c].XOffset;
            bmp.DrawInto(result, dx, dy + Fonts[fontId].Chars[c].YOffset);
            dx += bmp.Width;
        }

        return result;
    }
}
