using Moongate.Server.Ultima.Data.Templates.Books;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using Moongate.Core.Utils;
using Moongate.UoxItemConverter.Data.Internal.Books;
using Tomlyn;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Converts ModernUO's static server book texts into the existing Moongate document catalog.
/// </summary>
internal static class ModernUoBookConverter
{
    public static int Run(string source, string destination, TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(source))
        {
            error.WriteLine($"ModernUO source folder does not exist: {source}");
            return 2;
        }
        try
        {
            RejectLink(source);
            RejectLink(destination);
            if (File.Exists(destination)) throw new InvalidDataException($"Destination is a file: {destination}");
            var books = new Dictionary<string, ImportedBook>(StringComparer.Ordinal);
            foreach (var path in Sources(source).Order(StringComparer.Ordinal))
            {
                var text = File.ReadAllText(path, new UTF8Encoding(false, true));
                foreach (var book in ModernUoBookSourceReader.Read(text, path))
                {
                    if (!books.TryAdd(book.Id, book)) throw new InvalidDataException($"{path}: duplicate book id {book.Id}.");
                }
            }
            if (books.Count == 0) throw new InvalidDataException($"{source}: no static books found.");

            // Serialize and validate every target before touching earlier output.
            var files = new List<(string Path, string Text)>();
            foreach (var book in books.Values.OrderBy(book => book.Id, StringComparer.Ordinal))
            {
                var path = Path.Combine(destination, book.Id + ".toml");
                RejectLink(path);
                if (Directory.Exists(path)) throw new InvalidDataException($"Output file is a directory: {path}");
                var document = new ConvertedBookSource<BookTranslation>
                {
                    Title = EscapeDollars(book.Title), Author = EscapeDollars(book.Author), Content = EscapeDollars(book.Content), ItemId = book.ItemId,
                    Translations = ModernUoBookTranslationReader.Read(path, book)
                };
                files.Add((path, Serialize(document, book.Id)));
            }
            Directory.CreateDirectory(destination);
            foreach (var file in files) File.WriteAllText(file.Path, file.Text, new UTF8Encoding(false));
            output.WriteLine($"{books.Count} books, {books.Values.Sum(book => book.PageCount)} pages converted to {destination}");
            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            error.WriteLine($"Book conversion failed: {exception.Message}");
            return 2;
        }
    }

    private static string Serialize(ConvertedBookSource<BookTranslation> document, string id)
    {
        // Translated bodies stay editable multiline text when that keeps them exactly; else they are plain strings.
        var text = HexGraphic(TomlUtils.Serialize(new ConvertedBookSource<ConvertedBookTranslation>
        {
            Title = document.Title, Author = document.Author, Content = document.Content, ItemTemplate = document.ItemTemplate,
            ItemId = document.ItemId,
            Translations = document.Translations?.ToDictionary(pair => pair.Key, pair => new ConvertedBookTranslation
            {
                Title = pair.Value.Title, Author = pair.Value.Author, Content = pair.Value.Content
            }, StringComparer.Ordinal)
        }));
        if (MatchesSource(text, document)) return text;

        text = HexGraphic(TomlUtils.Serialize(document));
        if (MatchesSource(text, document)) return text;

        // Basic strings retain leading newlines and CR/CRLF sequences that multiline TOML normalizes.
        var fields = new Dictionary<string, object>
        {
            ["title"] = document.Title,
            ["author"] = document.Author,
            ["content"] = document.Content,
            ["item_template"] = document.ItemTemplate
        };
        if (document.ItemId is { } graphic) fields.Add("item_id", graphic);
        if (document.Translations is not null) fields.Add("translations", document.Translations);
        text = HexGraphic(TomlUtils.Serialize(fields));
        if (!MatchesSource(text, document))
            throw new InvalidDataException($"{id}: serialized TOML does not preserve the source text.");
        return text;
    }

    // A graphic reads as the other graphics of the templates do: item_id = 0x0FF1.
    private static string HexGraphic(string text)
    {
        return Regex.Replace(text, @"^item_id = (\d+)\r?$", match => $"item_id = 0x{int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture):X4}",
            RegexOptions.Multiline, TimeSpan.FromSeconds(1));
    }

    private static bool MatchesSource(string text, ConvertedBookSource<BookTranslation> expected)
    {
        try
        {
            var actual = TomlUtils.Deserialize<ConvertedBookSource<BookTranslation>>(text);
            return actual is not null && actual.Title == expected.Title && actual.Author == expected.Author &&
                   actual.Content == expected.Content && actual.ItemTemplate == expected.ItemTemplate && actual.ItemId == expected.ItemId &&
                   (actual.Translations?.Count ?? 0) == (expected.Translations?.Count ?? 0) &&
                   (expected.Translations is null || expected.Translations.All(pair =>
                       actual.Translations is not null && actual.Translations.TryGetValue(pair.Key, out var translation) &&
                       translation.Title == pair.Value.Title && translation.Author == pair.Value.Author &&
                       translation.Content == pair.Value.Content));
        }
        catch (TomlException)
        {
            return false;
        }
    }

    private static IEnumerable<string> Sources(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectLink(path);
            if (Directory.Exists(path))
            {
                foreach (var file in Sources(path)) yield return file;
            }
            else if (Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase)) yield return path;
        }
    }

    private static void RejectLink(string path)
    {
        if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
            throw new InvalidDataException($"Symbolic links are not supported: {path}");
    }

    private static string EscapeDollars(string text)
    {
        return text.Replace("$", "$$", StringComparison.Ordinal);
    }
}
