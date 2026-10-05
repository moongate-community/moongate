namespace Moongate.Server.Ultima.Data.Books;

public sealed record RenderedBook
{
    public string TemplateId { get; init; } = "";
    public string ItemTemplateId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string Content { get; init; } = "";
}
