using Moongate.Ultima.Imaging;

// ascii text support written by arul
namespace Moongate.Ultima;

public sealed class AsciiFont
{
    public byte Header { get; }
    public byte[] Unk { get; set; }
    public UltimaBitmap[] Characters { get; set; }
    public int Height { get; set; }

    public AsciiFont(byte header)
    {
        Header = header;
        Height = 0;
        Unk = new byte[224];
        Characters = new UltimaBitmap[224];
    }

    /// <summary>
    /// Gets Bitmap of given character
    /// </summary>
    /// <param name="character"></param>
    /// <returns></returns>
    public UltimaBitmap GetBitmap(char character)
    {
        return Characters[((character - 0x20) & 0x7FFFFFFF) % 224];
    }

    public int GetWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int width = 0;

        foreach (var character in text)
        {
            width += GetBitmap(character).Width;
        }

        return width;
    }

    public void ReplaceCharacter(int character, UltimaBitmap import)
    {
        Characters[character] = import;
        Height = import.Height;
    }

    public static AsciiFont GetFixed(int font, AsciiFont[] fonts)
    {
        if (font is < 0 or > 9)
        {
            font = 3;
        }

        return fonts[font];
    }
}
