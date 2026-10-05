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
                var document = new ConvertedBookSource
                {
                    Title = EscapeDollars(book.Title), Author = EscapeDollars(book.Author), Content = EscapeDollars(book.Content)
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

    private static string Serialize(ConvertedBookSource document, string id)
    {
        var text = TomlUtils.Serialize(document);
        if (MatchesSource(text, document)) return text;

        // Basic strings retain leading newlines and CR/CRLF sequences that multiline TOML normalizes.
        text = TomlUtils.Serialize(new Dictionary<string, string>
        {
            ["title"] = document.Title,
            ["author"] = document.Author,
            ["content"] = document.Content,
            ["item_template"] = document.ItemTemplate
        });
        if (!MatchesSource(text, document))
            throw new InvalidDataException($"{id}: serialized TOML does not preserve the source text.");
        return text;
    }

    private static bool MatchesSource(string text, ConvertedBookSource expected)
    {
        try
        {
            var actual = TomlUtils.Deserialize<ConvertedBookSource>(text);
            return actual is not null && actual.Title == expected.Title && actual.Author == expected.Author &&
                   actual.Content == expected.Content && actual.ItemTemplate == expected.ItemTemplate;
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
