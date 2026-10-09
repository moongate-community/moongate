using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Pets;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the kinds of food of <c>data/pet_food.toml</c>. The file may be missing: no pet eats then. An unknown kind,
///     a kind there twice or an item that is not an item template stop the server at startup, naming it.
/// </summary>
public class PetFoodLoader : IDataLoader<PetFood>
{
    private readonly ILogger _logger = Log.ForContext<PetFoodLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private string foodFilePath => Path.Join(_directoriesConfig["data"], "pet_food.toml");

    public PetFoodLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<PetFood>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(foodFilePath))
        {
            _logger.Information("No pet_food.toml: no pet eats");

            return new DataLoaderResult<PetFood> { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<PetFoodFile>(foodFilePath, null, cancellationToken) ??
                   new PetFoodFile();
        var itemIds = _dataLoaderService.GetEntities<ItemTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var food in file.Food)
        {
            if (!PetFoodKinds.All.Contains(food.Kind))
            {
                throw Invalid($"the kind '{food.Kind}' is not one of {string.Join(", ", PetFoodKinds.All)}");
            }

            if (!seen.Add(food.Kind))
            {
                throw Invalid($"the kind {food.Kind} is there twice");
            }

            if (food.Items.Find(item => !itemIds.Contains(item)) is { } unknown)
            {
                throw Invalid($"the item '{unknown}' of {food.Kind} is not an item template");
            }
        }

        _logger.Information("Found {Count} kinds of pet food", file.Food.Count);

        return new DataLoaderResult<PetFood> { Entities = file.Food };
    }

    private InvalidDataException Invalid(string reason)
    {
        return new($"{foodFilePath}: {reason}.");
    }
}
