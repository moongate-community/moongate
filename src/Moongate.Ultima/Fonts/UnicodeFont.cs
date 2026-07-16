namespace Moongate.Ultima.Fonts;

public sealed class UnicodeFont
{
    public UnicodeChar[] Chars { get; }

    public UnicodeFont()
    {
        Chars = new UnicodeChar[0x10000];
    }

    /// <summary>
    /// Returns max height of text
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public int GetHeight(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var height = 0;

        foreach (var character in text)
        {
            var c = character % 0x10000;
            height = Math.Max(height, Chars[c].Height + Chars[c].YOffset);
        }

        return height;
    }

    /// <summary>
    /// Returns width of text
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public int GetWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0;

        foreach (var character in text)
        {
            var c = character % 0x10000;
            width += Chars[c].Width;
            width += Chars[c].XOffset;
        }

        return width;
    }
}
