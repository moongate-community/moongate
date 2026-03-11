using System.Collections;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("text", "Provides text template rendering from scripts/texts.")]
public sealed class TextModule
{
    private readonly ITextTemplateService _textTemplateService;

    public TextModule(ITextTemplateService textTemplateService)
    {
        _textTemplateService = textTemplateService;
    }

    [ScriptFunction("render", "Renders a text template file using the provided context table.")]
    public string? Render(string relativePath, Table? context = null)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var model = context is null ? null : ConvertTable(context);

        return _textTemplateService.TryRender(relativePath.Trim(), model, out var rendered) ? rendered : null;
    }

    private static Dictionary<string, object?> ConvertTable(Table table)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in table.Pairs)
        {
            if (pair.Key.Type != DataType.String || string.IsNullOrWhiteSpace(pair.Key.String))
            {
                continue;
            }

            dictionary[pair.Key.String] = ConvertDynValue(pair.Value);
        }

        return dictionary;
    }

    private static object? ConvertDynValue(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean              => value.Boolean,
            DataType.Number               => value.Number,
            DataType.String               => value.String,
            DataType.Table                => ConvertTableOrArray(value.Table!),
            _                             => value.ToObject()
        };
    }

    private static object ConvertTableOrArray(Table table)
    {
        if (IsSequentialArray(table))
        {
            var list = new List<object?>(table.Length);

            for (var index = 1; index <= table.Length; index++)
            {
                list.Add(ConvertDynValue(table.Get(index)));
            }

            return list;
        }

        return ConvertTable(table);
    }

    private static bool IsSequentialArray(Table table)
    {
        if (table.Length <= 0)
        {
            return false;
        }

        foreach (var pair in table.Pairs)
        {
            if (pair.Key.Type != DataType.Number)
            {
                return false;
            }

            var numericKey = pair.Key.Number;
            var integerKey = Math.Truncate(numericKey);

            if (Math.Abs(numericKey - integerKey) > double.Epsilon || integerKey < 1 || integerKey > table.Length)
            {
                return false;
            }
        }

        return true;
    }
}
