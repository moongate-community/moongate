using System.Collections;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Scripting;
using Scriban;
using Scriban.Runtime;
using Serilog;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Renders text templates stored under scripts/texts using Scriban.
/// </summary>
public sealed class TextTemplateService : ITextTemplateService
{
    private static readonly ILogger Logger = Log.ForContext<TextTemplateService>();
    private readonly string _textsRootPath;
    private readonly MoongateConfig _config;

    public TextTemplateService(DirectoriesConfig directoriesConfig, MoongateConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        _textsRootPath = Path.Combine(directoriesConfig[DirectoryType.Scripts], "texts");
    }

    /// <inheritdoc />
    public bool TryRender(string relativePath, IReadOnlyDictionary<string, object?>? model, out string rendered)
    {
        rendered = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (!TryResolveTemplatePath(relativePath, out var templatePath))
        {
            Logger.Warning("Rejected text template path '{Path}'.", relativePath);

            return false;
        }

        if (!File.Exists(templatePath))
        {
            Logger.Warning("Text template '{Path}' was not found.", templatePath);

            return false;
        }

        var source = File.ReadAllText(templatePath);
        var template = Template.Parse(source, templatePath);

        if (template.HasErrors)
        {
            var firstError = template.Messages.Count > 0 ? template.Messages[0].Message : "unknown parse error";
            Logger.Error("Invalid text template '{Path}': {Error}", templatePath, firstError);

            return false;
        }

        var globals = CreateGlobalScriptObject(model);
        var context = new TemplateContext();
        context.PushGlobal(globals);

        try
        {
            rendered = template.Render(context);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to render text template '{Path}'.", templatePath);

            rendered = string.Empty;

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

    private bool TryResolveTemplatePath(string relativePath, out string templatePath)
    {
        templatePath = string.Empty;

        var normalized = relativePath.Trim();

        if (Path.IsPathRooted(normalized))
        {
            return false;
        }

        var fullRootPath = Path.GetFullPath(_textsRootPath);
        var candidatePath = Path.GetFullPath(Path.Combine(fullRootPath, normalized));

        if (!candidatePath.StartsWith(fullRootPath, StringComparison.Ordinal))
        {
            return false;
        }

        templatePath = candidatePath;

        return true;
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
}
