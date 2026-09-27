using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads <c>data/names.toml</c>, one <see cref="NameList" /> per list. Ids and names are trimmed; an empty or
///     duplicate id, an empty list or an empty name stops the server at startup.
/// </summary>
public class NamesLoader : IDataLoader<NameList>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<NamesLoader>();

    private string namesFilePath => Path.Join(_directoriesConfig["data"], "names.toml");

    public NamesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(namesFilePath))
        {
            throw new FileNotFoundException("Names file names.toml not found", namesFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<NameList>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var file = await TomlUtils.DeserializeFromFileAsync<NameListFile>(namesFilePath, null, cancellationToken) ??
                   new NameListFile();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var list in file.Names)
        {
            if (string.IsNullOrWhiteSpace(list.Id) || !ids.Add(list.Id.Trim()))
            {
                throw new InvalidDataException($"{namesFilePath}: name list id '{list.Id}' is empty or used twice.");
            }

            list.Id = list.Id.Trim();
            list.Names = list.Names.Select(name => name.Trim()).ToList();

            if (list.Names.Count == 0 || list.Names.Any(name => name.Length == 0))
            {
                throw new InvalidDataException($"{namesFilePath}: name list '{list.Id}' is empty or has an empty name.");
            }
        }

        _logger.Information(
            "Found {ListCount} name lists with {NameCount} names",
            file.Names.Count,
            file.Names.Sum(list => list.Names.Count)
        );

        return new DataLoaderResult<NameList>()
        {
            Entities = file.Names
        };
    }
}
