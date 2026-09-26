using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the container entries of <c>data/containers.toml</c>. Anything but exactly one default entry, an item id
///     listed by two entries, a gump id below 1 or an empty item area stops the server at startup.
/// </summary>
public class ContainersLoader : IDataLoader<ContainerContent>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<ContainersLoader>();

    private string containersFilePath => Path.Join(_directoriesConfig["data"], "containers.toml");

    public ContainersLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(containersFilePath))
        {
            throw new FileNotFoundException("Containers file containers.toml not found", containersFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<ContainerContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var containersFile =
            await TomlUtils.DeserializeFromFileAsync<ContainerContentFile>(containersFilePath, null, cancellationToken);
        var containers = containersFile?.Container ?? [];

        if (containers.Count(container => container.Default) != 1)
        {
            throw new InvalidDataException($"{containersFilePath} must mark exactly one [[container]] as default.");
        }

        var owners = new Dictionary<int, int>();

        foreach (var container in containers)
        {
            if (container.Gump < 1 || container.Bounds.Width < 1 || container.Bounds.Height < 1)
            {
                throw new InvalidDataException(
                    $"{containersFilePath}: gump 0x{container.Gump:X} needs a gump id and an item area of at least 1x1."
                );
            }

            foreach (var item in container.Items)
            {
                if (!owners.TryAdd(item, container.Gump))
                {
                    throw new InvalidDataException(
                        $"{containersFilePath}: item 0x{item:X} is listed by gump 0x{owners[item]:X} and gump 0x{container.Gump:X}."
                    );
                }
            }
        }

        _logger.Information("Found {Count} container gumps for {ItemCount} container items", containers.Count, owners.Count);

        return new DataLoaderResult<ContainerContent>()
        {
            Entities = containers
        };
    }
}
