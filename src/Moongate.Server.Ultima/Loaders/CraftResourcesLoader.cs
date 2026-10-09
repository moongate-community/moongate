using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Utils;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the resource lists of <c>data/crafts/resources.toml</c>. The file may be missing: no recipe can name a list
///     then. A list with a bad id or an id used twice, no template, or a template that is no item template stops the
///     server at startup.
/// </summary>
public class CraftResourcesLoader : IDataLoader<CraftResourceList>
{
    private readonly ILogger _logger = Log.ForContext<CraftResourcesLoader>();

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private string resourcesFilePath => Path.Join(_directoriesConfig["data"], "crafts", "resources.toml");

    public CraftResourcesLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<CraftResourceList>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(resourcesFilePath))
        {
            _logger.Information("No data/crafts/resources.toml: recipes can name item templates only");

            return new() { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<CraftResourcesFile>(resourcesFilePath, null, cancellationToken) ??
                   new CraftResourcesFile();
        var items = _dataLoaderService.GetEntities<ItemTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var list in file.Resource)
        {
            if (!ScriptIdUtils.IsValid(list.Id))
            {
                throw Invalid($"the resource id '{list.Id}' {ScriptIdUtils.Rule}");
            }

            if (!ids.Add(list.Id))
            {
                throw Invalid($"the resource {list.Id} is there twice");
            }

            if (list.Templates.Count == 0)
            {
                throw Invalid($"the resource {list.Id} has no template");
            }

            if (list.Templates.FirstOrDefault(template => !items.Contains(template)) is { } unknown)
            {
                throw Invalid($"the resource {list.Id} names '{unknown}', which is not an item template");
            }
        }

        _logger.Information("Found {Count} craft resource lists", file.Resource.Count);

        return new() { Entities = file.Resource };
    }

    private InvalidDataException Invalid(string reason)
    {
        return new($"{resourcesFilePath}: {reason}.");
    }
}
