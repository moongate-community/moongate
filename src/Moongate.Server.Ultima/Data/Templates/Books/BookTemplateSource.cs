namespace Moongate.Server.Ultima.Data.Templates.Books;

public class BookTemplateSource
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Variables { get; set; } = [];
    public string ItemTemplate { get; set; } = "readable_scroll";
    public Dictionary<string, BookTranslation> Translations { get; set; } = new(StringComparer.Ordinal);
}
