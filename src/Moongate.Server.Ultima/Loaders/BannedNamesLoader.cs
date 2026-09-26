using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads <c>data/banned_names.toml</c> as a single <see cref="BannedNamesContent" />. Words are trimmed; an empty
///     word stops the server at startup, since it would ban every name.
/// </summary>
public class BannedNamesLoader : IDataLoader<BannedNamesContent>
{
    private readonly DirectoriesConfig _directoriesConfig;

    private readonly ILogger _logger = Log.ForContext<BannedNamesLoader>();

    private string bannedNamesFilePath => Path.Join(_directoriesConfig["data"], "banned_names.toml");

    public BannedNamesLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(bannedNamesFilePath))
        {
            throw new FileNotFoundException("Banned names file banned_names.toml not found", bannedNamesFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<BannedNamesContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var bannedNames =
            await TomlUtils.DeserializeFromFileAsync<BannedNamesContent>(bannedNamesFilePath, null, cancellationToken) ??
            new BannedNamesContent();

        bannedNames.StartsWith = Normalize(bannedNames.StartsWith, "starts_with");
        bannedNames.Words = Normalize(bannedNames.Words, "words");

        _logger.Information(
            "Found {StartsWithCount} banned name prefixes and {WordCount} banned name words",
            bannedNames.StartsWith.Count,
            bannedNames.Words.Count
        );

        return new DataLoaderResult<BannedNamesContent>()
        {
            Entities = [bannedNames]
        };
    }

    private List<string> Normalize(List<string> words, string field)
    {
        var normalized = words.Select(word => word.Trim()).ToList();

        if (normalized.Any(word => word.Length == 0))
        {
            throw new InvalidDataException($"{bannedNamesFilePath}: '{field}' has an empty word, which would ban every name.");
        }

        return normalized;
    }
}
