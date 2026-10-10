using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Utils;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the spells of <c>data/spells.toml</c>. The file may be missing: no spell can be cast then. A spell with a
///     number outside 1 to 64, a number or a key used twice, a bad key, a circle outside 1 to 8, a reagent that is not an
///     item template or with no amount, or a scroll that is not an item template stops the server at startup.
/// </summary>
public class SpellsLoader : IDataLoader<SpellDefinition>
{
    public const int MaxSpellId = 64;
    public const int MaxCircle = 8;

    private readonly ILogger _logger = Log.ForContext<SpellsLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IDataLoaderService _dataLoaderService;

    private string spellsFilePath => Path.Join(_directoriesConfig["data"], "spells.toml");

    public SpellsLoader(DirectoriesConfig directoriesConfig, IDataLoaderService dataLoaderService)
    {
        _directoriesConfig = directoriesConfig;
        _dataLoaderService = dataLoaderService;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<SpellDefinition>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(spellsFilePath))
        {
            _logger.Information("No spells.toml: no spell can be cast");

            return new DataLoaderResult<SpellDefinition> { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<SpellsFile>(spellsFilePath, null, cancellationToken) ??
                   new SpellsFile();
        var templates = _dataLoaderService.GetEntities<ItemTemplate>()
            .Select(template => template.Id)
            .ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<int>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spell in file.Spell)
        {
            Validate(spell, templates, ids, keys);
        }

        _logger.Information("Found {Count} spells", file.Spell.Count);

        return new DataLoaderResult<SpellDefinition> { Entities = file.Spell };
    }

    private void Validate(SpellDefinition spell, HashSet<string> templates, HashSet<int> ids, HashSet<string> keys)
    {
        if (spell.Id is < 1 or > MaxSpellId)
        {
            throw Invalid($"the spell number {spell.Id} must be 1 to {MaxSpellId}");
        }

        if (!ids.Add(spell.Id))
        {
            throw Invalid($"the spell number {spell.Id} is there twice");
        }

        if (!ScriptIdUtils.IsValid(spell.Key))
        {
            throw Invalid($"the key '{spell.Key}' of spell {spell.Id} {ScriptIdUtils.Rule}");
        }

        if (!keys.Add(spell.Key))
        {
            throw Invalid($"the key {spell.Key} is there twice");
        }

        if (spell.Circle is < 1 or > MaxCircle)
        {
            throw Invalid($"the circle of {spell.Key} must be 1 to {MaxCircle}");
        }

        foreach (var reagent in spell.Reagents)
        {
            if (!templates.Contains(reagent.Template))
            {
                throw Invalid($"the reagent '{reagent.Template}' of {spell.Key} is not an item template");
            }

            if (reagent.Amount < 1)
            {
                throw Invalid($"the amount of the reagent {reagent.Template} of {spell.Key} must be at least 1");
            }
        }

        if (!templates.Contains(spell.Scroll))
        {
            throw Invalid($"the scroll '{spell.Scroll}' of {spell.Key} is not an item template");
        }
    }

    private InvalidDataException Invalid(string reason)
    {
        return new($"{spellsFilePath}: {reason}.");
    }
}
