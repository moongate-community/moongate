namespace Moongate.Server.Data.Internal.Scripting;

public sealed class BookTemplateContent
{
    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public bool? ReadOnly { get; set; }

    public string Content { get; set; } = string.Empty;
}
