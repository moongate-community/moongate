using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Text;
using Moongate.Server.Ultima.Types.Text;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads plain document sources once, after item templates are available.
/// </summary>
public sealed class BooksLoader : IDataLoader<BookTemplate>
{
    private readonly DirectoriesConfig _directories;
    private readonly IDataLoaderService _data;
    private readonly ITileDataService? _tiles;
    private readonly ILogger _logger = Log.ForContext<BooksLoader>();

    public BooksLoader(DirectoriesConfig directories, IDataLoaderService data, ITileDataService? tiles = null)
    {
        _directories = directories;
        _data = data;
        _tiles = tiles;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<BookTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Join(_directories["templates"], "books");
        var books = new Dictionary<string, BookTemplate>(StringComparer.Ordinal);
        if (!Directory.Exists(directory))
        {
            return new() { Entities = [] };
        }

        var items = _data.GetEntities<ItemTemplate>().ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.toml", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            BookTemplate book;
            try
            {
                book = await TomlUtils.DeserializeFromFileAsync<BookTemplate>(path, null, cancellationToken) ?? new();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidDataException($"{path}: invalid book TOML.", exception);
            }

            book.Id = Path.GetFileNameWithoutExtension(path);
            book.File = path;
            if (!TextTemplateTokens.IsValidName(book.Id))
            {
                throw new InvalidDataException($"{path}: invalid document id.");
            }

            if (!books.TryAdd(book.Id, book))
            {
                throw new InvalidDataException($"{path}: duplicate document id also defined in {books[book.Id].File}.");
            }

            Validate(book, items);
            BookAttachmentValidation.Validate(book, items, _tiles);
        }

        _logger.Information("Loaded {Count} document templates", books.Count);
        return new() { Entities = books.Values.ToList() };
    }

    private static void Validate(BookTemplate book, IReadOnlyDictionary<string, ItemTemplate> items)
    {
        var names = TextTemplateBuiltins.Values(new TextTemplateContext()).Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var name in book.Variables)
        {
            if (!TextTemplateTokens.IsValidName(name) || !names.Add(name))
            {
                throw new InvalidDataException($"{book.File}: invalid, duplicate or built-in variable declaration.");
            }
        }

        if (!items.TryGetValue(book.ItemTemplate, out var item) || item.Stackable != false ||
            !BookTextValidation.IsReadableScript(item.ScriptId))
        {
            throw new InvalidDataException($"{book.File}: item_template must select an explicitly nonstackable readable item.");
        }

        ValidateFields(book.File, book.Title, book.Author, book.Content, names);
        foreach (var (language, translation) in book.Translations)
        {
            if (language is not ("eng" or "ita" or "fre" or "ger" or "spa" or "por" or "pol" or "cze"))
            {
                throw new InvalidDataException($"{book.File}: unsupported translation language.");
            }

            ValidateFields(book.File, translation.Title ?? book.Title, translation.Author ?? book.Author,
                translation.Content ?? book.Content, names);
        }
    }

    private static void ValidateFields(string file, string title, string author, string content, HashSet<string> names)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException($"{file}: title and content must be nonblank.");
        }

        foreach (var (text, limit) in new[] { (title, int.MaxValue), (author, int.MaxValue), (content, BookTextValidation.ContentLimit) })
        {
            if (!BookTextValidation.IsValidText(text, limit))
            {
                throw new InvalidDataException($"{file}: document text contains forbidden controls or exceeds its source limit.");
            }

            foreach (var token in TextTemplateTokens.Find(text, TextTemplateSyntaxType.Document))
            {
                if (token.Name is { } name && (!TextTemplateTokens.IsValidName(name) || !names.Contains(name)))
                {
                    throw new InvalidDataException($"{file}: invalid or unknown document variable.");
                }
            }
        }
    }
}
