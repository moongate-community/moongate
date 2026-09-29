using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Titles;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads and validates the fame and karma title grid during server startup.
/// </summary>
public sealed class TitlesLoader : IDataLoader<FameKarmaTitle>
{
    private readonly DirectoriesConfig _directories;
    private readonly ILogger _logger = Log.ForContext<TitlesLoader>();

    public TitlesLoader(DirectoriesConfig directories)
    {
        _directories = directories;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<FameKarmaTitle>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_directories["data"], "titles.toml");

        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{path}: title file is missing.");
        }

        TitlesFile file;

        try
        {
            file = await TomlUtils.DeserializeFromFileAsync<TitlesFile>(path, null, cancellationToken) ?? new TitlesFile();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException($"{path}: invalid titles TOML.", exception);
        }

        if (file.Titles is not { Count: > 0 })
        {
            throw new InvalidDataException($"{path}: 'titles' must be a nonempty array.");
        }

        var rows = new List<FameKarmaTitle>(file.Titles.Count);
        var pairs = new HashSet<(int Fame, int Karma)>();
        var fames = new HashSet<int>();
        var karmas = new HashSet<int>();

        for (var index = 0; index < file.Titles.Count; index++)
        {
            var definition = file.Titles[index];
            var where = $"{path}: titles row {index + 1}";

            if (definition is null || definition.Fame is null || definition.Karma is null || definition.Title is null)
            {
                throw new InvalidDataException($"{where} requires fame, karma and title.");
            }

            if (definition.Title.Length > 0 && string.IsNullOrWhiteSpace(definition.Title) ||
                definition.FemaleTitle is { Length: > 0 } && string.IsNullOrWhiteSpace(definition.FemaleTitle))
            {
                throw new InvalidDataException($"{where} has a whitespace-only title.");
            }

            var fame = definition.Fame.Value;
            var karma = definition.Karma.Value;

            if (!pairs.Add((fame, karma)))
            {
                throw new InvalidDataException($"{path}: duplicate title pair fame={fame}, karma={karma}.");
            }

            fames.Add(fame);
            karmas.Add(karma);
            rows.Add(new FameKarmaTitle(fame, karma, definition.Title.Trim(), definition.FemaleTitle?.Trim()));
        }

        foreach (var fame in fames)
        {
            foreach (var karma in karmas)
            {
                if (!pairs.Contains((fame, karma)))
                {
                    throw new InvalidDataException($"{path}: missing title pair fame={fame}, karma={karma}.");
                }
            }
        }

        _logger.Information("Loaded {Count} fame and karma titles from {Path}", rows.Count, path);

        return new() { Entities = rows };
    }
}
