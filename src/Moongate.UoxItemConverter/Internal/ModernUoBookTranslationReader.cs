using System.Text;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Text;
using Moongate.Server.Ultima.Types.Text;
using Moongate.UoxItemConverter.Data.Internal.Books;
using Tomlyn;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Preserves validated translation overrides when refreshing an imported English source.
/// </summary>
internal static class ModernUoBookTranslationReader
{
    public static Dictionary<string, BookTranslation>? Read(string path, ImportedBook book)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var source = TomlUtils.Deserialize<BookTemplateSource>(File.ReadAllText(path, utf8))
                         ?? throw new InvalidDataException("Existing document cannot be deserialized.");
            if (source.Translations.Count == 0) return null;

            var translations = new Dictionary<string, BookTranslation>(StringComparer.Ordinal);
            foreach (var (language, translation) in source.Translations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (language is not ("eng" or "ita" or "fre" or "ger" or "spa" or "por" or "pol" or "cze"))
                    throw new InvalidDataException($"Unsupported translation language: {language}.");
                var title = translation.Title ?? book.Title.Replace("$", "$$", StringComparison.Ordinal);
                var author = translation.Author ?? book.Author.Replace("$", "$$", StringComparison.Ordinal);
                var content = translation.Content ?? book.Content.Replace("$", "$$", StringComparison.Ordinal);
                foreach (var (text, limit) in new[]
                             { (title, int.MaxValue), (author, int.MaxValue), (content, BookTextValidation.ContentLimit) })
                {
                    utf8.GetByteCount(text);
                    if (!BookTextValidation.IsValidText(text, limit) ||
                        TextTemplateTokens.Find(text, TextTemplateSyntaxType.Document).Any(token => token.Name is not null))
                        throw new InvalidDataException($"Invalid literal translation text: {language}.");
                }

                var values = new Dictionary<string, string>();
                title = TextTemplateRenderer.Render(title, values, TextTemplateSyntaxType.Document);
                author = TextTemplateRenderer.Render(author, values, TextTemplateSyntaxType.Document);
                content = TextTemplateRenderer.Render(content, values, TextTemplateSyntaxType.Document);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content) ||
                    !BookGumpRenderer.TryBuild(title, author, content, out _) || !BookPagination.TryPaginate(content, out _))
                    throw new InvalidDataException($"Translation exceeds document or packet limits: {language}.");
                translations.Add(language, translation);
            }

            return translations;
        }
        catch (Exception exception) when (exception is InvalidDataException or TomlException or EncoderFallbackException
                                              or DecoderFallbackException)
        {
            throw new InvalidDataException($"{path}: {exception.Message}", exception);
        }
    }
}
