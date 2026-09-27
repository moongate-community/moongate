using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads every <c>*.toml</c> under <c>templates/items/</c>, recursively, and resolves <c>base_id</c>: a nullable field
///     left unset takes the parent's value, up the chain; <c>item_id</c> 0 takes the parent's; the child's <c>tags</c>
///     replace the parent's. An empty or duplicate id, a missing parent or a cycle stops the server at startup.
/// </summary>
public class ItemTemplatesLoader : IDataLoader<ItemTemplate>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<ItemTemplatesLoader>();

    private string itemsDirectoryPath => Path.Join(_directoriesConfig["templates"], "items");

    public ItemTemplatesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<ItemTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var byId = new Dictionary<string, (ItemTemplate Template, string File)>(StringComparer.Ordinal);

        if (Directory.Exists(itemsDirectoryPath))
        {
            foreach (var path in Directory.EnumerateFiles(itemsDirectoryPath, "*.toml", SearchOption.AllDirectories)
                                          .Order(StringComparer.Ordinal))
            {
                var file = await TomlUtils.DeserializeFromFileAsync<ItemTemplateFile>(path, null, cancellationToken) ??
                           new ItemTemplateFile();

                foreach (var template in file.Item)
                {
                    if (string.IsNullOrWhiteSpace(template.Id))
                    {
                        throw new InvalidDataException($"{path}: an item template has no id.");
                    }

                    if (!byId.TryAdd(template.Id, (template, path)))
                    {
                        throw new InvalidDataException(
                            $"Item template id '{template.Id}' is defined in both {byId[template.Id].File} and {path}."
                        );
                    }
                }
            }
        }

        var resolved = new Dictionary<string, ItemTemplate>(StringComparer.Ordinal);

        foreach (var id in byId.Keys)
        {
            Resolve(id, byId, resolved, []);
        }

        foreach (var template in resolved.Values)
        {
            template.Validate();
        }

        _logger.Information("Found {Count} item templates", resolved.Count);

        return new() { Entities = byId.Keys.Select(id => resolved[id]).ToList() };
    }

    private static ItemTemplate Resolve(
        string id,
        Dictionary<string, (ItemTemplate Template, string File)> byId,
        Dictionary<string, ItemTemplate> resolved,
        HashSet<string> visiting
    )
    {
        if (resolved.TryGetValue(id, out var done))
        {
            return done;
        }

        if (!visiting.Add(id))
        {
            throw new InvalidDataException($"Item template '{id}' inherits from itself through base_id.");
        }

        var (template, file) = byId[id];

        if (template.BaseId is { } baseId)
        {
            if (!byId.ContainsKey(baseId))
            {
                throw new InvalidDataException($"{file}: item template '{id}' has base_id '{baseId}', which does not exist.");
            }

            Inherit(template, Resolve(baseId, byId, resolved, visiting));
        }

        visiting.Remove(id);
        resolved[id] = template;

        return template;
    }

    // Hue and rarity always have a value, so the child's own is kept.
    private static void Inherit(ItemTemplate child, ItemTemplate parent)
    {
        if (child.ItemId.Value == 0)
        {
            child.ItemId = parent.ItemId;
        }

        if (string.IsNullOrEmpty(child.ScriptId))
        {
            child.ScriptId = parent.ScriptId;
        }

        child.Name ??= parent.Name;
        child.Movable ??= parent.Movable;
        child.Weight ??= parent.Weight;
        child.Amount ??= parent.Amount;
        child.Stackable ??= parent.Stackable;
        child.Layer ??= parent.Layer;
        child.BuyPrice ??= parent.BuyPrice;
        child.SellPrice ??= parent.SellPrice;
        child.Decays ??= parent.Decays;
        child.DecayMinutes ??= parent.DecayMinutes;
        child.LootType ??= parent.LootType;
        // A copy, so changing one template's tags never changes its parent's or a sibling's.
        child.Tags ??= parent.Tags is null ? null : new Dictionary<string, string>(parent.Tags);
        child.Visibility ??= parent.Visibility;
        child.MaxItems ??= parent.MaxItems;
        child.MaxWeight ??= parent.MaxWeight;
    }
}
