using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Loads public moongate definitions from <c>scripts/moongates/data.lua</c>.
/// </summary>
public sealed class PublicMoongateDefinitionService : IPublicMoongateDefinitionService
{
    private static readonly Dictionary<string, int> MapNameToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["felucca"] = 0,
        ["trammel"] = 1,
        ["ilshenar"] = 2,
        ["malas"] = 3,
        ["tokuno"] = 4,
        ["termur"] = 5,
        ["ter_mur"] = 5,
        ["internal"] = 0x7F
    };

    private readonly ILogger _logger = Log.ForContext<PublicMoongateDefinitionService>();
    private readonly DirectoriesConfig _directoriesConfig;

    public PublicMoongateDefinitionService(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public IReadOnlyList<PublicMoongateGroupDefinition> Load()
    {
        var scriptPath = Path.Combine(_directoriesConfig[DirectoryType.Scripts], "moongates", "data.lua");

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Public moongate data file not found.", scriptPath);
        }

        var script = new Script();
        DynValue module;

        try
        {
            module = script.DoString(File.ReadAllText(scriptPath), null, "scripts/moongates/data.lua");
        }
        catch (InterpreterException ex)
        {
            _logger.Error(ex, "Failed to load public moongate data from {ScriptPath}", scriptPath);

            throw new InvalidOperationException($"Failed to load public moongate data from '{scriptPath}'.", ex);
        }

        if (module.Type != DataType.Table || module.Table is null)
        {
            throw new InvalidOperationException("Public moongate data script must return a table.");
        }

        var loadValue = module.Table.Get("load");

        if (loadValue.Type != DataType.Function)
        {
            throw new InvalidOperationException("Public moongate data script must expose a 'load' function.");
        }

        var result = script.Call(loadValue);

        if (result.Type != DataType.Table || result.Table is null)
        {
            throw new InvalidOperationException("Public moongate data 'load' must return a table.");
        }

        return [.. result.Table.Pairs
                          .OrderBy(static pair => pair.Key.CastToNumber())
                          .Select(static pair => pair.Value)
                          .Where(static value => value.Type == DataType.Table && value.Table is not null)
                          .Select(ParseGroup)];
    }

    private static PublicMoongateGroupDefinition ParseGroup(DynValue value)
    {
        var table = value.Table!;
        var id = RequireString(table, "id", "Public moongate group is missing required string field 'id'.");
        var name = RequireString(table, "name", $"Public moongate group '{id}' is missing required string field 'name'.");
        var destinationsValue = table.Get("destinations");

        if (destinationsValue.Type != DataType.Table || destinationsValue.Table is null)
        {
            throw new InvalidOperationException($"Public moongate group '{id}' must define a 'destinations' table.");
        }

        var destinations = destinationsValue.Table.Pairs
                                         .OrderBy(static pair => pair.Key.CastToNumber())
                                         .Select(static pair => pair.Value)
                                         .Where(static destination => destination.Type == DataType.Table && destination.Table is not null)
                                         .Select(destination => ParseDestination(id, destination))
                                         .ToList();

        return new PublicMoongateGroupDefinition(id, name, destinations);
    }

    private static PublicMoongateDestinationDefinition ParseDestination(string groupId, DynValue value)
    {
        var table = value.Table!;
        var id = RequireString(
            table,
            "id",
            $"Public moongate destination in group '{groupId}' is missing required string field 'id'."
        );
        var name = RequireString(
            table,
            "name",
            $"Public moongate destination '{id}' in group '{groupId}' is missing required string field 'name'."
        );
        var mapRaw = RequireString(
            table,
            "map",
            $"Public moongate destination '{id}' in group '{groupId}' is missing required string field 'map'."
        );

        if (!TryResolveMapId(mapRaw, out var mapId))
        {
            throw new InvalidOperationException(
                $"Public moongate destination '{id}' in group '{groupId}' references unknown map '{mapRaw}'."
            );
        }

        var x = RequireInt(table, "x", $"Public moongate destination '{id}' in group '{groupId}' is missing numeric field 'x'.");
        var y = RequireInt(table, "y", $"Public moongate destination '{id}' in group '{groupId}' is missing numeric field 'y'.");
        var z = RequireInt(table, "z", $"Public moongate destination '{id}' in group '{groupId}' is missing numeric field 'z'.");

        return new PublicMoongateDestinationDefinition(id, name, mapId, new(x, y, z));
    }

    private static string RequireString(Table table, string fieldName, string message)
    {
        var value = table.Get(fieldName);

        if (value.Type != DataType.String || string.IsNullOrWhiteSpace(value.String))
        {
            throw new InvalidOperationException(message);
        }

        return value.String.Trim();
    }

    private static int RequireInt(Table table, string fieldName, string message)
    {
        var value = table.Get(fieldName);

        if (value.Type != DataType.Number)
        {
            throw new InvalidOperationException(message);
        }

        return Convert.ToInt32(value.Number);
    }

    private static bool TryResolveMapId(string rawValue, out int mapId)
    {
        var normalized = rawValue.Trim();

        if (MapNameToId.TryGetValue(normalized, out mapId))
        {
            return true;
        }

        return int.TryParse(normalized, out mapId);
    }
}
