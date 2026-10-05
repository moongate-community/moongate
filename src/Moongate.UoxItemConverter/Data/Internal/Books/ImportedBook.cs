namespace Moongate.UoxItemConverter.Data.Internal.Books;

internal sealed class ImportedBook
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required string Content { get; init; }
    public required int PageCount { get; init; }
}
