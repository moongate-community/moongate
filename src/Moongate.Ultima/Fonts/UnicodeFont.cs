using System;

namespace Moongate.Ultima.Fonts;

public sealed class UnicodeFont
{
    public UnicodeChar[] Chars { get; }

    public UnicodeFont()
    {
        Chars = new UnicodeChar[0x10000];
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

        int width = 0;
        foreach (var character in text)
        {
            int c = character % 0x10000;
            width += Chars[c].Width;
            width += Chars[c].XOffset;
        }

        return width;
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

        int height = 0;
        foreach (var character in text)
        {
            int c = character % 0x10000;
            height = Math.Max(height, Chars[c].Height + Chars[c].YOffset);
        }

        return height;
    }
}
