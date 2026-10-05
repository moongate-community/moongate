using System.Globalization;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Text;
using Moongate.Server.Ultima.Types.Text;
using Serilog;

namespace Moongate.Server.Ultima.Services.Books;

/// <inheritdoc />
public sealed class BookTemplateService : IBookTemplateService
{
    private readonly IDataLoaderService _data;
    private readonly ILogger _logger = Log.ForContext<BookTemplateService>();

    public BookTemplateService(IDataLoaderService data)
    {
        _data = data;
    }

    public bool TryGet(string id, out BookTemplate? template)
    {
        template = _data.GetEntities<BookTemplate>().FirstOrDefault(book => string.Equals(book.Id, id, StringComparison.Ordinal));
        return template is not null;
    }

    public bool TryRender(string id, TextTemplateContext context, string language, IReadOnlyDictionary<string, object?>? values, out RenderedBook? rendered)
    {
        rendered = null;
        if (!TryGet(id, out var template) || template is null)
        {
            return Fail(id, "unknown template");
        }

        var expanded = new Dictionary<string, string>(TextTemplateBuiltins.Values(context), StringComparer.Ordinal);
        if ((values?.Count ?? 0) != template.Variables.Count)
        {
            return Fail(id, "missing or extra values");
        }

        foreach (var name in template.Variables)
        {
            if (values is null || !values.TryGetValue(name, out var value) || !TryFormat(value, out var text) ||
                !BookTextValidation.IsValidText(text, BookTextValidation.ContentLimit) ||
                !expanded.TryAdd(name, text))
            {
                return Fail(id, "invalid custom value");
            }
        }

        template.Translations.TryGetValue(language, out var translation);
        var title = TextTemplateRenderer.Render(translation?.Title ?? template.Title, expanded, TextTemplateSyntaxType.Document);
        var author = TextTemplateRenderer.Render(translation?.Author ?? template.Author, expanded, TextTemplateSyntaxType.Document);
        var content = TextTemplateRenderer.Render(translation?.Content ?? template.Content, expanded, TextTemplateSyntaxType.Document);
        if (string.IsNullOrWhiteSpace(title) ||
            !BookTextValidation.IsValidText(title, BookTextValidation.HeaderLimit) ||
            !BookTextValidation.IsValidText(author, BookTextValidation.HeaderLimit) ||
            !BookTextValidation.IsValidText(content, BookTextValidation.ContentLimit))
        {
            return Fail(id, "invalid rendered fields");
        }

        rendered = new() { TemplateId = id, ItemTemplateId = template.ItemTemplate, Title = title, Author = author, Content = content };
        return true;
    }

    private static bool TryFormat(object? value, out string text)
    {
        text = value switch
        {
            string result => result,
            bool flag => flag ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong or decimal => ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
            double number when double.IsFinite(number) => number.ToString(CultureInfo.InvariantCulture),
            float number when float.IsFinite(number) => number.ToString(CultureInfo.InvariantCulture),
            _ => ""
        };
        return value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal ||
            value is double doubleValue && double.IsFinite(doubleValue) || value is float floatValue && float.IsFinite(floatValue);
    }

    private bool Fail(string id, string reason)
    {
        _logger.Warning("Cannot render document {TemplateId}: {Reason}", id, reason);
        return false;
    }
}
