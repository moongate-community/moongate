namespace Moongate.Server.Ultima.Services.Books;

public static class BookTextValidation
{
    public const int HeaderLimit = 128;
    public const int ContentLimit = 16384;

    public static bool IsValidText(string text, int limit)
    {
        return text.Length <= limit && !text.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t');
    }

    public static bool IsReadableScript(string? script)
    {
        return script is "readable_scroll" or "jail_note";
    }
}
