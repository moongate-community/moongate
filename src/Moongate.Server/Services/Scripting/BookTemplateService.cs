using System.Collections;
using System.Text;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Scriban;
using Scriban.Runtime;
using Serilog;

namespace Moongate.Server.Services.Scripting;

public sealed class BookTemplateService : IBookTemplateService
{
    private static readonly ILogger Logger = Log.ForContext<BookTemplateService>();
    private readonly string _booksRootPath;
    private readonly MoongateConfig _config;

    public BookTemplateService(DirectoriesConfig directoriesConfig, MoongateConfig config)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _booksRootPath = Path.Combine(directoriesConfig[DirectoryType.Templates], "books");
    }

    public bool TryLoad(string bookId, IReadOnlyDictionary<string, object?>? model, out BookTemplateContent? book)
    {
        book = null;

        if (string.IsNullOrWhiteSpace(bookId))
        {
            return false;
        }

        if (!TryResolveBookPath(bookId, out var templatePath))
        {
            Logger.Warning("Rejected book template path '{BookId}'.", bookId);
            return false;
        }

        if (!File.Exists(templatePath))
        {
            Logger.Warning("Book template '{Path}' was not found.", templatePath);
            return false;
        }

        var source = PreprocessTemplateSource(File.ReadAllText(templatePath));

        if (!TryParseMetadata(source, out var title, out var author, out var readOnly, out var body))
        {
            Logger.Error("Invalid book template '{Path}': missing [Title] or [Author], or invalid [ReadOnly].", templatePath);
            return false;
        }

        var titleTemplate = Template.Parse(title, templatePath);
        var authorTemplate = Template.Parse(author, templatePath);
        var bodyTemplate = Template.Parse(body, templatePath);

        if (titleTemplate.HasErrors || authorTemplate.HasErrors || bodyTemplate.HasErrors)
        {
            var firstError = titleTemplate.Messages.Concat(authorTemplate.Messages)
                                         .Concat(bodyTemplate.Messages)
                                         .FirstOrDefault()
                                         ?.Message ?? "unknown parse error";
            Logger.Error("Invalid book template '{Path}': {Error}", templatePath, firstError);
            return false;
        }

        var context = new TemplateContext();
        context.PushGlobal(CreateGlobalScriptObject(model));

        try
        {
            book = new BookTemplateContent
            {
                Title = titleTemplate.Render(context).Trim(),
                Author = authorTemplate.Render(context).Trim(),
                ReadOnly = readOnly,
                Content = bodyTemplate.Render(context).Trim()
            };

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to render book template '{Path}'.", templatePath);
            return false;
        }
    }

    private ScriptObject CreateGlobalScriptObject(IReadOnlyDictionary<string, object?>? model)
    {
        var globals = new ScriptObject
        {
            ["shard"] = new ScriptObject
            {
                ["name"] = _config.Game.ShardName,
                ["website_url"] = _config.Http.WebsiteUrl
            }
        };

        if (model is null)
        {
            return globals;
        }

        foreach (var (key, value) in model)
        {
            globals[key] = ConvertValue(value);
        }

        return globals;
    }

    private bool TryResolveBookPath(string bookId, out string templatePath)
    {
        templatePath = string.Empty;

        var normalized = bookId.Trim();

        if (Path.IsPathRooted(normalized))
        {
            return false;
        }

        var fileName = normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? normalized : $"{normalized}.txt";
        var fullRootPath = Path.GetFullPath(_booksRootPath);
        var candidatePath = Path.GetFullPath(Path.Combine(fullRootPath, fileName));

        if (!candidatePath.StartsWith(fullRootPath, StringComparison.Ordinal))
        {
            return false;
        }

        templatePath = candidatePath;
        return true;
    }

    private static bool TryParseMetadata(
        string source,
        out string title,
        out string author,
        out bool? readOnly,
        out string body
    )
    {
        title = string.Empty;
        author = string.Empty;
        readOnly = null;
        body = string.Empty;

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal)
                          .Replace('\r', '\n')
                          .Split('\n');
        var bodyLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("[Title]", StringComparison.OrdinalIgnoreCase))
            {
                title = line[7..].Trim();
                continue;
            }

            if (line.StartsWith("[Author]", StringComparison.OrdinalIgnoreCase))
            {
                author = line[8..].Trim();
                continue;
            }

            if (line.StartsWith("[ReadOnly]", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[10..].Trim();

                if (!bool.TryParse(value, out var parsed))
                {
                    return false;
                }

                readOnly = parsed;
                continue;
            }

            bodyLines.Add(line);
        }

        body = string.Join('\n', bodyLines).Trim();

        return !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(author);
    }

    private static object? ConvertValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> readonlyDictionary)
        {
            var scriptObject = new ScriptObject();

            foreach (var (key, nestedValue) in readonlyDictionary)
            {
                scriptObject[key] = ConvertValue(nestedValue);
            }

            return scriptObject;
        }

        if (value is IDictionary dictionary)
        {
            var scriptObject = new ScriptObject();

            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key)
                {
                    continue;
                }

                scriptObject[key] = ConvertValue(entry.Value);
            }

            return scriptObject;
        }

        if (value is string)
        {
            return value;
        }

        if (value is IEnumerable enumerable)
        {
            var array = new ScriptArray();

            foreach (var item in enumerable)
            {
                array.Add(ConvertValue(item));
            }

            return array;
        }

        return value;
    }

    private static string PreprocessTemplateSource(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal)
                          .Replace('\r', '\n')
                          .Split('\n');
        var processedLines = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                processedLines.Add(string.Empty);
                continue;
            }

            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            processedLines.Add(StripInlineComment(line));
        }

        return string.Join('\n', processedLines);
    }

    private static string StripInlineComment(string line)
    {
        var builder = new StringBuilder(line.Length);
        var escaped = false;

        foreach (var character in line)
        {
            if (escaped)
            {
                if (character != '#')
                {
                    builder.Append('\\');
                }

                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '#')
            {
                break;
            }

            builder.Append(character);
        }

        if (escaped)
        {
            builder.Append('\\');
        }

        return builder.ToString().TrimEnd();
    }
}
