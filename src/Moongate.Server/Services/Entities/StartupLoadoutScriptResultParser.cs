using System.Text.Json;
using Moongate.Server.Data.Startup;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Parses the Lua startup loadout hook result into a validated startup loadout.
/// </summary>
public static class StartupLoadoutScriptResultParser
{
    /// <summary>
    /// Parses a Lua table result into a startup loadout.
    /// </summary>
    /// <param name="result">Lua table returned by <c>build_starting_loadout</c>.</param>
    /// <returns>Validated startup loadout.</returns>
    public static StartupLoadout Parse(Table result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var loadout = new StartupLoadout();

        ParseSection(result, "backpack", loadout.Backpack);
        ParseSection(result, "equip", loadout.Equip);

        return loadout;
    }

    private static void ParseSection(Table result, string sectionName, List<StartupLoadoutItem> destination)
    {
        var sectionValue = result.Get(sectionName);

        if (sectionValue.Type is DataType.Nil or DataType.Void)
        {
            return;
        }

        if (sectionValue.Type != DataType.Table || sectionValue.Table is null)
        {
            throw new InvalidOperationException(
                $"Lua starting loadout section '{sectionName}' must be a table."
            );
        }

        foreach (var pair in sectionValue.Table.Pairs.OrderBy(static pair => pair.Key.CastToNumber()))
        {
            if (pair.Value.Type != DataType.Table || pair.Value.Table is null)
            {
                throw new InvalidOperationException(
                    $"Lua starting loadout entry in '{sectionName}' must be a table."
                );
            }

            destination.Add(ParseItem(sectionName, pair.Value.Table));
        }
    }

    private static StartupLoadoutItem ParseItem(string sectionName, Table itemTable)
    {
        var templateId = itemTable.Get("template_id");

        if (templateId.Type != DataType.String || string.IsNullOrWhiteSpace(templateId.String))
        {
            throw new InvalidOperationException(
                $"Lua starting loadout entry in '{sectionName}' is missing required string field 'template_id'."
            );
        }

        var amount = 1;
        var amountValue = itemTable.Get("amount");

        if (amountValue.Type != DataType.Nil && amountValue.Type != DataType.Void)
        {
            if (amountValue.Type != DataType.Number)
            {
                throw new InvalidOperationException(
                    $"Lua starting loadout entry '{templateId.String}' has invalid non-numeric 'amount'."
                );
            }

            amount = Convert.ToInt32(amountValue.Number);

            if (amount <= 0)
            {
                throw new InvalidOperationException(
                    $"Lua starting loadout entry '{templateId.String}' must have a positive 'amount'."
                );
            }
        }

        JsonElement? args = null;
        var argsValue = itemTable.Get("args");

        if (argsValue.Type != DataType.Nil && argsValue.Type != DataType.Void)
        {
            if (argsValue.Type != DataType.Table || argsValue.Table is null)
            {
                throw new InvalidOperationException(
                    $"Lua starting loadout entry '{templateId.String}' has invalid 'args'; expected table."
                );
            }

            args = JsonSerializer.SerializeToElement(ConvertTable(argsValue.Table));
        }

        ItemLayerType? layer = null;
        var layerValue = itemTable.Get("layer");

        if (layerValue.Type != DataType.Nil && layerValue.Type != DataType.Void)
        {
            if (layerValue.Type != DataType.String ||
                string.IsNullOrWhiteSpace(layerValue.String) ||
                !Enum.TryParse<ItemLayerType>(layerValue.String, true, out var parsedLayer))
            {
                throw new InvalidOperationException(
                    $"Lua starting loadout entry '{templateId.String}' has invalid 'layer'."
                );
            }

            layer = parsedLayer;
        }

        return new()
        {
            TemplateId = templateId.String!,
            Amount = amount,
            Args = args,
            Layer = layer
        };
    }

    private static object? ConvertDynValue(DynValue value)
        => value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean => value.Boolean,
            DataType.String => value.String,
            DataType.Number => ConvertNumber(value.Number),
            DataType.Table when value.Table is not null => ConvertTable(value.Table),
            _ => throw new InvalidOperationException(
                $"Unsupported Lua value type '{value.Type}' in startup loadout args."
            )
        };

    private static object ConvertNumber(double number)
    {
        var rounded = Math.Round(number);
        return Math.Abs(number - rounded) < double.Epsilon
            ? Convert.ToInt64(rounded)
            : number;
    }

    private static object ConvertTable(Table table)
    {
        var isArray = table.Pairs.All(
            static pair => pair.Key.Type == DataType.Number &&
                           Math.Abs(pair.Key.Number - Math.Round(pair.Key.Number)) < double.Epsilon &&
                           pair.Key.Number >= 1);

        if (isArray)
        {
            return table.Pairs
                        .OrderBy(static pair => pair.Key.Number)
                        .Select(static pair => ConvertDynValue(pair.Value))
                        .ToList();
        }

        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in table.Pairs)
        {
            if (pair.Key.Type != DataType.String || string.IsNullOrWhiteSpace(pair.Key.String))
            {
                throw new InvalidOperationException(
                    "Lua startup loadout args tables must use string keys for object-style entries."
                );
            }

            dictionary[pair.Key.String] = ConvertDynValue(pair.Value);
        }

        return dictionary;
    }
}
