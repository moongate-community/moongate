using System.Globalization;
using System.Text;
using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the English texts and replaces them with those of the configured language. A language's texts are
///     <c>data/messages/&lt;language&gt;.toml</c> plus every toml file in <c>data/messages/&lt;language&gt;/</c>,
///     merged.
///     A number that is not a message id, a text that is not a valid composite format, the same id in two files of a
///     language, a translation that needs more values than the English text or a translated id missing from English
///     stops the server at startup.
/// </summary>
public class MessagesLoader : IDataLoader<MessageContent>
{
    private const string EnglishLanguage = "eng";

    private const int MaxValues = 16;

    private static readonly EnumerationOptions TomlFiles = new() { MatchCasing = MatchCasing.CaseInsensitive };

    private readonly DirectoriesConfig _directoriesConfig;

    private readonly LocalizationConfig _localizationConfig;

    private readonly ILogger _logger = Log.ForContext<MessagesLoader>();

    private string language => _localizationConfig.Language.ToLowerInvariant();

    public MessagesLoader(DirectoriesConfig directoriesConfig, LocalizationConfig localizationConfig)
    {
        _directoriesConfig = directoriesConfig;
        _localizationConfig = localizationConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var languageCode in new[] { EnglishLanguage, language })
        {
            if (GetPaths(languageCode).Count == 0)
            {
                throw new FileNotFoundException(
                    $"Messages of {languageCode} not found: neither {languageCode}.toml nor a toml file in {languageCode}/",
                    GetPath(languageCode)
                );
            }
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<MessageContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var english = await ReadLanguageAsync(EnglishLanguage, cancellationToken);

        if (english.Count == 0)
        {
            throw new InvalidDataException(
                $"The {EnglishLanguage} messages have no [messages] entries: {string.Join(", ", GetPaths(EnglishLanguage))}."
            );
        }

        var translated = new Dictionary<int, string>();

        if (language != EnglishLanguage)
        {
            foreach (var (id, (text, path)) in await ReadLanguageAsync(language, cancellationToken))
            {
                if (!english.TryGetValue(id, out var englishMessage))
                {
                    throw new InvalidDataException($"{path}: message {id} is not in the {EnglishLanguage} messages.");
                }

                if (CountValues(text) > CountValues(englishMessage.Text))
                {
                    throw new InvalidDataException(
                        $"{path}: message {id} needs more values than the English text '{englishMessage.Text}'."
                    );
                }

                translated[id] = text;
            }
        }

        _logger.Information(
            "Found {Count} messages in {Language}, {Fallback} of them in English",
            english.Count,
            language,
            language == EnglishLanguage ? 0 : english.Count - translated.Count
        );

        return new DataLoaderResult<MessageContent>()
        {
            Entities = english.OrderBy(message => message.Key)
                .Select(message => new MessageContent
                    {
                        Id = message.Key,
                        Text = translated.GetValueOrDefault(message.Key, message.Value.Text)
                    }
                )
                .ToList()
        };
    }

    private string GetPath(string languageCode)
    {
        return Path.Join(_directoriesConfig["data"], "messages", $"{languageCode}.toml");
    }

    /// <summary>
    ///     Returns the language's file when it exists, then the toml files of the language's directory in name order.
    /// </summary>
    private List<string> GetPaths(string languageCode)
    {
        var paths = new List<string>();
        var file = GetPath(languageCode);
        var directory = Path.Join(_directoriesConfig["data"], "messages", languageCode);

        if (File.Exists(file))
        {
            paths.Add(file);
        }

        if (Directory.Exists(directory))
        {
            paths.AddRange(Directory.EnumerateFiles(directory, "*.toml", TomlFiles).Order(StringComparer.Ordinal));
        }

        return paths;
    }

    /// <summary>
    ///     Reads every file of the language into one set, each text with the file it comes from.
    /// </summary>
    private async Task<Dictionary<int, (string Text, string Path)>> ReadLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken
    )
    {
        var messages = new Dictionary<int, (string Text, string Path)>();

        foreach (var path in GetPaths(languageCode))
        {
            foreach (var (id, text) in await ReadAsync(path, cancellationToken))
            {
                if (messages.TryGetValue(id, out var existing))
                {
                    throw new InvalidDataException($"{path}: message {id} is already in {existing.Path}.");
                }

                messages[id] = (text, path);
            }
        }

        return messages;
    }

    private static async Task<Dictionary<int, string>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var file = await TomlUtils.DeserializeFromFileAsync<MessageContentFile>(path, null, cancellationToken);
        var messages = new Dictionary<int, string>();

        foreach (var (key, text) in file?.Messages ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                throw new InvalidDataException($"{path}: '{key}' is not a message number.");
            }

            if (string.IsNullOrEmpty(text) || CountValues(text) < 0)
            {
                throw new InvalidDataException(
                    $"{path}: message {id} must be a text with at most {MaxValues} values, written {{0}}, {{1}}, ..."
                );
            }

            if (!messages.TryAdd(id, text))
            {
                throw new InvalidDataException($"{path}: message {id} is written more than once.");
            }
        }

        return messages;
    }

    /// <summary>
    ///     Returns how many values the text needs to be formatted, or -1 when it is not a valid composite format.
    /// </summary>
    private static int CountValues(string text)
    {
        try
        {
            var count = CompositeFormat.Parse(text).MinimumArgumentCount;

            return count <= MaxValues ? count : -1;
        }
        catch (FormatException)
        {
            return -1;
        }
    }
}
